# Insurance claim analysis on AKS

A claims analysis demo for AKS. Use synthetic data only. Run locally or configure your own Azure environment.

## Analyze a claim

Open the root HTTPS address in a browser. **ClaimLens** accepts amounts in US dollars, converts them to integer cents and calls the API on the same origin. Load a synthetic example or enter your own figures, then select **Analyze claim** to see the estimated payout and review reasons. Editing the inputs clears the previous result; service errors never produce an estimate. The frontend is plain HTML, CSS and JavaScript in [`src/Claims/wwwroot`](src/Claims/wwwroot), served by the same app.

To use the API directly:

```powershell
$url = gh variable get CLAIMS_URL
$claim = @{ lossCents=900000; deductibleCents=50000; limitCents=500000; daysToReport=31 }
Invoke-RestMethod "$url/claims/analyze" -Method Post -ContentType application/json -Body ($claim | ConvertTo-Json)
```

```json
{"estimatedPayableCents":500000,"queue":"adjuster-review","reasons":["POLICY_LIMIT_APPLIED","LATE_REPORT"]}
```

`POST /claims/analyze` accepts exactly these four integer fields. Loss and limit must be 1 to 1,000,000,000 cents; the deductible may also be zero. Days to report must be 0 to 3,650. The service subtracts the deductible, floors the estimate at zero and caps it at the policy limit. Zero net loss, a binding cap or reporting after day 30 routes a claim for review; otherwise the queue is `standard`. Bad input returns 400, oversized bodies 413, other methods 405 and non-JSON submissions 415. `GET /healthz` checks availability; `GET /version` identifies the build.

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/Claims/` | ASP.NET Core app: `Program.cs` (HTTP contract and security headers), `ClaimAnalysis.cs` (rules and validation), `wwwroot/` (frontend). |
| `tests/Claims.Tests/` | xUnit tests against the in-memory host (`WebApplicationFactory`). |
| `tests/smoke.py` | Dependency-free end-to-end test of a running deployment (local container or public URL). |
| `Dockerfile` | Multi-stage build: .NET 11 SDK → `aspnet:11.0-resolute-chiseled`, non-root, port 8080. |
| `infra/main.bicep` | AKS, ACR, GitHub identity, federated credential and Azure role assignments. |
| `deploy/k8s/` | `app.yaml` (Deployment, Service, PodDisruptionBudget, NetworkPolicy) and `gateway.yaml` (Gateway, HTTPRoutes, Let's Encrypt Issuer). |
| `.github/workflows/dotnet-container-build.yml` | **Reusable** test → image → hardened smoke test → push to ACR. |
| `.github/workflows/aks-deploy.yml` | **Reusable** render manifests → apply to AKS → wait → verify endpoint. |
| `.github/workflows/build.yml`, `deploy.yml` | This repository's callers: CI/CD and manual redeploy/rollback. |
| `.github/agents/` | Five custom Copilot agents that repeat this modernization on other repositories, pinned to [Copilot Fusion](#replicate-this-process-with-copilot-agents-and-skills). |
| `.github/skills/` | Seven Copilot skills (runbook and one per phase) the agents load on demand. |

## Run locally

With the .NET 11 SDK and Python 3:

```powershell
dotnet test Claims.slnx
dotnet run --project src/Claims          # http://localhost:8080
python tests/smoke.py http://localhost:8080   # in a second terminal
```

With Docker: `docker build -t claims .` then `docker run --rm --read-only -p 127.0.0.1:8080:8080 claims`.

## Build, deploy and roll back

- **Pull requests** run [`build`](.github/workflows/build.yml): unit tests, image build, and the smoke test against the container running read-only as an arbitrary non-root UID. Nothing is published.
- **Pushes to `main`** (or `gh workflow run build.yml`) with `ACR_NAME` configured additionally push the tested image to ACR from the `aks-demo` environment, then deploy it by digest. The deploy waits for the rollout, the Gateway and the TLS certificate, polls `https://…/version` until it reports the new release, then runs the smoke test against the public URL. A green build alone does not count as a successful deployment.
- **Rollback**: `gh workflow run deploy.yml -f image=<registry>.azurecr.io/claims@sha256:<digest>` with a digest from an earlier run summary.

GitHub never holds cluster or registry secrets. The federated credential only trusts jobs running in the `aks-demo` environment, whose deployment-branch policy limits which branches can deploy. Inside the cluster the identity is **Azure Kubernetes Service RBAC Admin** on the `claims` namespace only.

### Reuse the workflows in another repository

```yaml
jobs:
  build:
    uses: melabadi/openshift-to-aks-public/.github/workflows/dotnet-container-build.yml@<commit-sha>
    permissions: { contents: read, id-token: write }
    with:
      image-name: my-api
      solution: MyApi.slnx
      publish: ${{ github.event_name != 'pull_request' }}
      environment: production
      registry-name: ${{ vars.ACR_NAME }}
      azure-client-id: ${{ vars.AZURE_CLIENT_ID }}
      azure-tenant-id: ${{ vars.AZURE_TENANT_ID }}
      azure-subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}
  deploy:
    needs: build
    uses: melabadi/openshift-to-aks-public/.github/workflows/aks-deploy.yml@<commit-sha>
    permissions: { contents: read, id-token: write }
    with:
      environment: production
      azure-client-id: ${{ vars.AZURE_CLIENT_ID }}
      azure-tenant-id: ${{ vars.AZURE_TENANT_ID }}
      azure-subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}
      resource-group: ${{ vars.AKS_RESOURCE_GROUP }}
      cluster-name: ${{ vars.AKS_CLUSTER }}
      namespace: my-api
      manifests: k8s/app.yaml
      manifest-vars: IMAGE=${{ needs.build.outputs.image }}
      rollout: deployment/my-api
```

Manifests use `${NAME}` placeholders; only the names listed in `manifest-vars` are substituted, and the job fails if any placeholder remains. The calling repository needs its own Azure identity with a federated credential for its environment (`infra/main.bicep` shows the pattern). If this repository is private, allow access to its workflows under **Settings → Actions → General → Access**.

## Replicate this process with Copilot agents and skills

The five **Copilot Fusion** agents and seven skills can be copied independently of the ClaimLens app, workflows and infrastructure. They target AKS, but do not assume a particular repository layout, language, CI provider or public endpoint. The .NET upgrade phase applies only to .NET workloads.

### Agents (`.github/agents/`)

| Agent | Role | Fusion tier (`model`, in preference order) |
| --- | --- | --- |
| [`aks-modernization-lead`](.github/agents/aks-modernization-lead.agent.md) | Plans, delegates and integrates all phases; owns the definition of done. | Max: `hydrafusion-max`, `hydrafusion` |
| [`dotnet-modernizer`](.github/agents/dotnet-modernizer.agent.md) | Assesses and upgrades the app to the latest .NET, tests, hardened container, smoke test. | High: `hydrafusion`, `hydrafusion-max` |
| [`aks-platform-engineer`](.github/agents/aks-platform-engineer.agent.md) | Bicep platform (AKS, ACR, GitHub OIDC identity) and one-time cluster bootstrap. | High: `hydrafusion`, `hydrafusion-max` |
| [`delivery-pipeline-engineer`](.github/agents/delivery-pipeline-engineer.agent.md) | Reusable build/deploy callers, manifests, GitHub environment and variables, first deploy. | High: `hydrafusion`, `hydrafusion-max` |
| [`modernization-verifier`](.github/agents/modernization-verifier.agent.md) | Independent read-only review and live verification with a go/no-go. | Max: `hydrafusion-max`, `hydrafusion` |

Planning and review use the Max tier; implementation uses High. Every agent sets `model-policy: required` so the Fusion list is not silently replaced.

### Skills (`.github/skills/`)

| Skill | Purpose |
| --- | --- |
| [`modernize-to-aks`](.github/skills/modernize-to-aks/SKILL.md) | Discover the target workload and coordinate the migration from assessment to verified release. |
| [`dotnet-latest-upgrade`](.github/skills/dotnet-latest-upgrade/SKILL.md) | Assess, upgrade, test and containerize a .NET app. |
| [`aks-bicep-platform`](.github/skills/aks-bicep-platform/SKILL.md) | Validate an existing AKS target or provision an approved one using the team's IaC. |
| [`aks-cluster-bootstrap`](.github/skills/aks-cluster-bootstrap/SKILL.md) | Check namespace, identity, image access and, only where needed, routing and TLS. |
| [`reusable-aks-pipelines`](.github/skills/reusable-aks-pipelines/SKILL.md) | Callers for the reusable workflows, manifests, environment and variables. |
| [`aks-verify-and-rollback`](.github/skills/aks-verify-and-rollback/SKILL.md) | Prove the release live and roll back by digest. |
| [`copilot-fusion-routing`](.github/skills/copilot-fusion-routing/SKILL.md) | How Fusion is selected, tier mapping, overrides and troubleshooting. |

### Run it on another project

1. Make the assets available to that project, using any one of:
   - **Repository:** copy `.github/agents` and `.github/skills` into it.
   - **User:** copy them to `~/.copilot/agents` and `~/.copilot/skills`.
   - **Organization:** publish the agent files in `agents/` in the organization's `.github-private` repository and install the skills at repository or user scope.
2. Start in the **target checkout** and name the workload if more than one ships from it. No configuration file is required. For an assessment without changes:

```powershell
copilot config fusionTier max
copilot --agent aks-modernization-lead --model fusion `
  -p "Assess the billing worker for AKS. Use the modernize-to-aks skill. Show how it builds, deploys and proves it processed work. Do not change files or Azure resources."
```

In the Copilot app, choose **Copilot Fusion** and `aks-modernization-lead`. The lead shares a [repository profile](.github/skills/modernize-to-aks/SKILL.md#build-a-repository-profile-first) with the specialists: project paths, build and test commands, delivery method, approved AKS target, and the evidence needed for that workload. For example, a private worker can reuse a shared cluster and prove a processed event without creating a public Gateway.

To proceed, confirm the workload, environment and permitted changes. The agents use the target repository's own assets first. If none fit, they can use a team-approved template at a reviewed revision or create local assets. Access to this repository is **not** required unless you explicitly choose to [reuse its workflows](#reuse-the-workflows-in-another-repository).

### How Fusion is enforced, and its limits

- Agent frontmatter uses the Fusion model ids `hydrafusion` (High) and `hydrafusion-max` (Max) in an ordered list with `model-policy: required`. The CLI and app model name `fusion` is **not** accepted as an agent `model` value (the agent is deselected), so the files use the ids.
- **Verified on Copilot CLI 1.0.114** under an administrator-managed `auto` / Fusion High setting: selecting each agent logs `Model changed from Copilot Fusion (High) to HydraFusion Max` for `aks-modernization-lead` and `modernization-verifier`, and `… to HydraFusion` for the three specialists. All seven skills were discovered by the lead agent.
- Managed policy still sets the session baseline (`copilot config fusionTier standard|high|max`); a stricter policy can override an agent's `model`.
- If an account has no Fusion access, a `required` agent falls back to the default agent. To allow another model, remove `model-policy: required` and add a model id from `/model` to the list.
- Which underlying models Fusion routes to is internal to Copilot; these files pin the tier, not the models.

See [`copilot-fusion-routing`](.github/skills/copilot-fusion-routing/SKILL.md) for troubleshooting.

## Bootstrap a fresh environment

Prerequisites: signed-in `az` (with `az aks install-cli` for `kubectl`/`kubelogin`), `helm`, `gh`, permission to create role assignments, and Kubernetes 1.33+ quota for two `Standard_D4ds_v5` nodes in the region.

```powershell
$rg = 'example-aks-rg'; $repo = 'OWNER/REPO'
$region = 'replace-with-supported-region'
az group create -n $rg -l $region
# Edit infra/main.bicepparam first: githubSubjectPrefix comes from
#   gh api repos/$repo/actions/oidc/customization/sub --jq .sub_claim_prefix
$out = az deployment group create -g $rg -n claims-platform -p infra/main.bicepparam `
  -p clusterAdminPrincipalId=$(az ad signed-in-user show --query id -o tsv) --query properties.outputs -o json | ConvertFrom-Json

# One-time platform setup by a cluster admin.
az aks get-credentials -g $rg -n $out.clusterName.value; kubelogin convert-kubeconfig -l azurecli
kubectl create namespace claims
az role assignment create --assignee-object-id $out.githubPrincipalId --assignee-principal-type ServicePrincipal `
  --role 'Azure Kubernetes Service RBAC Admin' --scope $out.appNamespaceScope.value
helm upgrade --install cert-manager oci://quay.io/jetstack/charts/cert-manager --namespace cert-manager --create-namespace `
  --set crds.enabled=true --set config.apiVersion=controller.config.cert-manager.io/v1alpha1 `
  --set config.kind=ControllerConfiguration --set config.enableGatewayAPI=true

# GitHub environment and variables (choose a DNS label unique in the region).
gh api -X PUT repos/$repo/environments/aks-demo -F deployment_branch_policy[protected_branches]=false -F deployment_branch_policy[custom_branch_policies]=true
gh api -X POST repos/$repo/environments/aks-demo/deployment-branch-policies -f name=main -f type=branch
$label = 'claims-unique-label'
gh variable set AZURE_CLIENT_ID -R $repo --body $out.githubClientId.value
gh variable set AZURE_TENANT_ID -R $repo --body (az account show --query tenantId -o tsv)
gh variable set AZURE_SUBSCRIPTION_ID -R $repo --body (az account show --query id -o tsv)
gh variable set ACR_NAME -R $repo --body $out.registryName.value
gh variable set AKS_RESOURCE_GROUP -R $repo --body $rg
gh variable set AKS_CLUSTER -R $repo --body $out.clusterName.value
gh variable set DNS_LABEL -R $repo --body $label
gh variable set APP_HOST -R $repo --body "$label.$region.cloudapp.azure.com"
gh variable set CLAIMS_URL -R $repo --body "https://$label.$region.cloudapp.azure.com"
gh workflow run build.yml -R $repo
```

Platform notes:

- **Ingress** uses the AKS [application routing Gateway API implementation](https://learn.microsoft.com/azure/aks/app-routing-gateway-api) (`gatewayClassName: approuting-istio`) with managed Gateway API CRDs, rather than the app routing NGINX add-on, which Microsoft documents as retiring in November 2026. The public hostname comes from the Azure DNS label annotation on the Gateway's load balancer; replace it with your own domain for production.
- **TLS** certificates are issued by Let's Encrypt through cert-manager's Gateway API integration (HTTP-01). For a dry run, switch `ACME_SERVER` to `https://acme-staging-v02.api.letsencrypt.org/directory` to avoid production rate limits.
- **Cluster**: Microsoft Entra ID with Azure RBAC and local accounts disabled, Azure CNI overlay with Cilium, Azure Linux nodes across three zones with autoscaling, workload identity enabled, and automatic `stable` Kubernetes and `NodeImage` upgrades.

To retire the environment, delete the resource group (`az group delete -n example-aks-rg`), the `aks-demo` GitHub environment and the repository variables above.
