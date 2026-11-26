// Azure platform for the claims app: AKS (Entra ID + Azure RBAC, managed Gateway API ingress),
// a private-pull Azure Container Registry, and a GitHub Actions identity federated to one GitHub environment.
targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short lowercase prefix used to name resources.')
@minLength(3)
@maxLength(12)
param namePrefix string = 'claims'

@description('Globally unique Azure Container Registry name (alphanumeric).')
param registryName string = '${namePrefix}${uniqueString(resourceGroup().id)}'

@description('GitHub OIDC subject prefix, from: gh api repos/OWNER/REPO/actions/oidc/customization/sub --jq .sub_claim_prefix')
param githubSubjectPrefix string

@description('GitHub environment whose jobs may publish images and deploy to the cluster.')
param githubEnvironment string = 'aks-demo'

@description('Kubernetes namespace the GitHub identity may manage.')
param appNamespace string = 'claims'

@description('Object ID of a user or group that administers the cluster (bootstrap and break-glass). Leave empty to skip.')
param clusterAdminPrincipalId string = ''

@allowed(['User', 'Group', 'ServicePrincipal'])
param clusterAdminPrincipalType string = 'User'

@description('Kubernetes minor version; empty selects the region default.')
param kubernetesVersion string = ''

param nodeVmSize string = 'Standard_D4ds_v5'
@minValue(1)
param nodeMinCount int = 2
@minValue(1)
param nodeMaxCount int = 4

@allowed(['Free', 'Standard'])
param clusterTier string = 'Free'

var roles = {
  acrPull: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
  acrPush: '8311e382-0749-4cb8-b61a-304f252e45ec'
  aksClusterUser: '4abbcc35-e782-43d8-92c5-2d3f1bd2253f'
  aksRbacClusterAdmin: 'b1ff04bb-8a4e-4dc4-8eb5-8693973ce19b'
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  sku: { name: 'Standard' }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

resource cluster 'Microsoft.ContainerService/managedClusters@2026-08-01' = {
  name: 'aks-${namePrefix}'
  location: location
  sku: { name: 'Base', tier: clusterTier }
  identity: { type: 'SystemAssigned' }
  properties: {
    dnsPrefix: 'aks-${namePrefix}'
    kubernetesVersion: empty(kubernetesVersion) ? null : kubernetesVersion
    enableRBAC: true
    disableLocalAccounts: true
    aadProfile: {
      managed: true
      enableAzureRBAC: true
      tenantID: tenant().tenantId
    }
    agentPoolProfiles: [
      {
        name: 'system'
        mode: 'System'
        osType: 'Linux'
        osSKU: 'AzureLinux'
        vmSize: nodeVmSize
        count: nodeMinCount
        enableAutoScaling: true
        minCount: nodeMinCount
        maxCount: nodeMaxCount
        availabilityZones: ['1', '2', '3']
        upgradeSettings: { maxSurge: '33%' }
      }
    ]
    networkProfile: {
      networkPlugin: 'azure'
      networkPluginMode: 'overlay'
      networkDataplane: 'cilium'
      networkPolicy: 'cilium'
      loadBalancerSku: 'standard'
      outboundType: 'loadBalancer'
    }
    oidcIssuerProfile: { enabled: true }
    securityProfile: {
      workloadIdentity: { enabled: true }
      imageCleaner: { enabled: true, intervalHours: 168 }
    }
    autoUpgradeProfile: {
      upgradeChannel: 'stable'
      nodeOSUpgradeChannel: 'NodeImage'
    }
    // Managed Gateway API CRDs plus the application routing Gateway API (sidecar-less Istio) implementation.
    ingressProfile: {
      gatewayAPI: { installation: 'Standard' }
      webAppRouting: {
        gatewayAPIImplementations: {
          appRoutingIstio: { mode: 'Enabled' }
        }
      }
    }
  }
}

// Nodes pull images from ACR with the kubelet identity; no pull secrets are stored in the cluster.
resource kubeletAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, cluster.id, roles.acrPull)
  scope: registry
  properties: {
    principalId: cluster.properties.identityProfile.kubeletidentity.objectId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPull)
  }
}

resource github 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${namePrefix}-github'
  location: location
}

resource githubFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: github
  name: 'github-${githubEnvironment}'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: '${githubSubjectPrefix}:environment:${githubEnvironment}'
    audiences: ['api://AzureADTokenExchange']
  }
}

resource githubAcrPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, github.id, roles.acrPush)
  scope: registry
  properties: {
    principalId: github.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPush)
  }
}

// Lets the workflow fetch a kubeconfig; Kubernetes permissions are granted per namespace (see README).
resource githubClusterUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(cluster.id, github.id, roles.aksClusterUser)
  scope: cluster
  properties: {
    principalId: github.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.aksClusterUser)
  }
}

resource adminClusterUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(clusterAdminPrincipalId)) {
  name: guid(cluster.id, clusterAdminPrincipalId, roles.aksClusterUser)
  scope: cluster
  properties: {
    principalId: clusterAdminPrincipalId
    principalType: clusterAdminPrincipalType
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.aksClusterUser)
  }
}

resource adminRbacClusterAdmin 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(clusterAdminPrincipalId)) {
  name: guid(cluster.id, clusterAdminPrincipalId, roles.aksRbacClusterAdmin)
  scope: cluster
  properties: {
    principalId: clusterAdminPrincipalId
    principalType: clusterAdminPrincipalType
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.aksRbacClusterAdmin)
  }
}

output clusterName string = cluster.name
output clusterId string = cluster.id
output registryName string = registry.name
output registryLoginServer string = registry.properties.loginServer
output githubClientId string = github.properties.clientId
output githubPrincipalId string = github.properties.principalId
output appNamespaceScope string = '${cluster.id}/namespaces/${appNamespace}'
