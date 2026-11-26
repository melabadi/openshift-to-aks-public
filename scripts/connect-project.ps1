#requires -Version 7.4
param(
    [Parameter(Mandatory)][string]$ResourceGroup,
    [Parameter(Mandatory)][string]$Cluster,
    [Parameter(Mandatory)][string]$Registry,
    [Parameter(Mandatory)][string]$Repository,
    [string]$RegistryResourceGroup = $ResourceGroup,
    [switch]$HostedControlPlane,
    [string]$Oc = 'oc'
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$namespace = 'insurance-claims'
$tokenName = 'openshift-claims-pull'
$previousConfig = $env:KUBECONFIG
$env:KUBECONFIG = Join-Path ([IO.Path]::GetTempPath()) ("claims-{0}.kubeconfig" -f [Guid]::NewGuid())
try {
    if ($HostedControlPlane) {
        az aro hcp cluster request-credential -g $ResourceGroup -n $Cluster --admin --file $env:KUBECONFIG --only-show-errors -o none
    } else {
        az aro get-admin-kubeconfig -g $ResourceGroup -n $Cluster --file $env:KUBECONFIG --only-show-errors -o none
    }
    if (-not (& $Oc get namespace $namespace --ignore-not-found -o name)) {
        & $Oc create namespace $namespace
    }
    $server = az acr show -g $RegistryResourceGroup -n $Registry --query loginServer -o tsv
    $tokens = az acr token list -r $Registry --query '[].name' -o tsv
    if ($tokens -notcontains $tokenName) {
        az acr token create -r $Registry -n $tokenName --repository claims content/read metadata/read --no-passwords -o none
    }
    # Never write the registry password to a file, CLI argument or GitHub secret.
    $password = az acr token credential generate -r $Registry -n $tokenName --password1 --expiration-in-days 90 --query 'passwords[0].value' -o tsv
    $auth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${tokenName}:${password}"))
    $docker = @{auths=@{$server=@{auth=$auth}}} | ConvertTo-Json -Depth 5 -Compress
    $secret = @{apiVersion='v1';kind='Secret';type='kubernetes.io/dockerconfigjson';metadata=@{name='claims-pull'};stringData=@{'.dockerconfigjson'=$docker}}
    $secret | ConvertTo-Json -Depth 5 -Compress | & $Oc -n $namespace apply --server-side --field-manager=claims-bootstrap -f -
    & $Oc -n $namespace secrets link default claims-pull --for=pull
    $manifest = Get-Content -Raw (Join-Path $PSScriptRoot '../openshift/app.yaml')
    $manifest.Replace('REGISTRY_HOST', $server) | & $Oc -n $namespace apply -f -
    $route = & $Oc -n $namespace get route claims -o 'jsonpath={.spec.host}'
    gh variable set CLAIMS_URL --repo $Repository --body "https://$route"
    Write-Output "Route: https://$route"
} finally {
    if (Test-Path -LiteralPath $env:KUBECONFIG) { Remove-Item -LiteralPath $env:KUBECONFIG }
    $env:KUBECONFIG = $previousConfig
}
