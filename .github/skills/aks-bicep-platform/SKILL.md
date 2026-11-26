---
name: aks-bicep-platform
description: Validate an existing AKS target or provision an approved one with infrastructure as code, using the consuming team's modules, identity and resource boundaries.
---

# Prepare the AKS platform

Use the platform fields in the `modernize-to-aks` profile. Confirm subscription and resource IDs with the owner. An existing CLI login identifies the operator, not the intended deployment target.

**Existing cluster or centrally managed landing zone:** inspect only the approved target. For example, use `az aks show --subscription <subscription> -g <group> -n <cluster>` to confirm the cluster ID, node resource group and configuration; obtain the registry login server and identity scopes from the owner or deployment outputs. Check reachability from the release runner, supported node OS/architecture, network policy, and ingress only if requested. Record gaps for the owner; do not deploy a duplicate cluster, registry or controller.

**New platform:** extend the target repository's IaC or approved platform modules. If none exist and Bicep is appropriate, create a local module with parameters for location, names, environment, scale, network and identity. Do not import another team's resource names, deployment outputs, API versions or role assignments as defaults.

1. Check region quotas, node SKUs/zones, supported Kubernetes versions (`az aks get-versions -l <region>`), connectivity, and cost. Review current [AKS guidance](https://learn.microsoft.com/azure/aks/) for the chosen configuration.
2. Use Entra authentication, scoped authorization and no local admin credentials. Configure image pull for the kubelet and push for the publisher according to the registry's authorization mode. Configure CI federation against the actual issuer, subject and audience for the approved repo/environment; derive these rather than guessing a subject from a repo name. Keep application workload identity separate from CI identity.
3. For resource-group-scoped Bicep, compile (`az bicep build --file <entrypoint>`) and preview (`az deployment group what-if --subscription <subscription> -g <group> --parameters <parameter-file.bicepparam>` when using a `using` parameter file). Use the correct deployment scope for the module. Review creates, changes, deletions, role scopes and cost before an approved apply; with another IaC tool use its equivalent plan/apply.
4. Map the real outputs to the handoff: cluster ID/name, actual node resource group, namespace, registry login server, CI client/principal IDs, and who grants access. Record provisioning state and resource links. Do not print tokens or kubeconfigs.
