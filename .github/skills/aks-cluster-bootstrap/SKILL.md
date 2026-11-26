---
name: aks-cluster-bootstrap
description: Check workload readiness on an AKS cluster and perform only the namespace, identity and optional routing setup approved by its owner.
---

# Prepare the namespace

Use the confirmed cluster ID and ownership boundary from the `modernize-to-aks` profile. Use a dedicated temporary kubeconfig outside the checkout for direct access (`az aks get-credentials --subscription <subscription> -g <group> -n <cluster> --file <private-file>`); point `KUBECONFIG` at it and confirm the context before any write. Use Entra authentication, not admin credentials. Remove the temporary file after use.

| Check | Action when missing |
| --- | --- |
| Namespace, policy, quotas and dependencies | Check the intended namespace. Create or label it only if the platform owner delegates that operation. Respect existing policy and tenants. |
| Delivery identity | Determine whether CI applies resources or a GitOps controller reconciles them. Give only the writer the Kubernetes permissions required by the package. For direct apply with Azure RBAC, scope the approved role to `<cluster-id>/namespaces/<namespace>`; fetching user credentials is a separate permission. Do not grant cluster admin to CI. |
| Image access | Verify the target nodes can pull the approved registry image. Don't grant publish rights to a pull identity. |
| Network and certificates | For a routed service, discover the existing Gateway/Ingress class, private or public DNS, and TLS provider. Reuse platform-managed facilities. For a worker with no inbound traffic, skip ingress, DNS and certificate setup. |

Where a new installation is explicitly approved, pin the compatible controller/chart version and follow its current installation guide. For example, if the selected stack is Gateway API plus cert-manager, verify the CRDs and accepted GatewayClass, enable the appropriate Gateway integration and check controller readiness. Do not install cluster-wide software via an application release job.

Return context/namespace evidence, role scope and an access check for the **actual deploying identity** (an administrator's `kubectl auth can-i` is not proof), registry readiness and any owner-managed prerequisites still pending.
