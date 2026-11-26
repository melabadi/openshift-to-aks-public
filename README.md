# Insurance claim analysis on OpenShift

A small web workspace and stateless HTTP service estimate payable loss and identify claims for an adjuster to review. No claimant identifiers, documents or claim history are required or stored. **Use synthetic data only.** The public demo has no user authentication and does not determine coverage or authorize payment.

No live deployment is included. Run locally or configure your own Azure resources and GitHub Actions variables.

## Analyze a claim

Open the deployment's root HTTPS address in a browser. **ClaimLens** accepts amounts in US dollars, converts them to integer cents, and calls the real API on the same origin. Load a synthetic example or enter your own figures, then select **Analyze claim** to see the estimated payout and review reasons. Editing the inputs clears the previous result; service errors never produce an estimate.

The responsive frontend uses plain HTML, CSS and JavaScript in `src/Claims/Web`, embedded in the existing executable. It requires JavaScript but no frontend framework, CDN, additional service or separate deployment. `GET /` now serves this page instead of the original JSON service description; the analysis, health and version API contracts are unchanged.

To use the API directly:

```powershell
$url = gh variable get CLAIMS_URL
$claim = @{ lossCents=900000; deductibleCents=50000; limitCents=500000; daysToReport=31 }
Invoke-RestMethod "$url/claims/analyze" -Method Post -ContentType application/json -Body ($claim | ConvertTo-Json)
```

```json
{"estimatedPayableCents":500000,"queue":"adjuster-review","reasons":["POLICY_LIMIT_APPLIED","LATE_REPORT"]}
```

`POST /claims/analyze` accepts exactly these four integer fields. Loss and limit must be 1 to 1,000,000,000 cents; the deductible may also be zero. Days to report must be 0 to 3,650. The service subtracts the deductible, floors the estimate at zero and caps it at the policy limit. Zero net loss, a binding cap or reporting after day 30 routes a claim for review; otherwise the queue is `standard`. Bad input returns 400, oversized bodies 413, and non-JSON submissions 415. `GET /healthz` checks availability; `GET /version` identifies the build.

## Run locally

On Windows with a .NET SDK, a compatible .NET Framework runtime and Python 3:

```powershell
dotnet publish src/Claims/Claims.csproj -c Release -o dist
.\dist\Claims.exe
# Open http://localhost:8080 in your browser.
# In a second terminal:
python tests/smoke.py http://localhost:8080
```

For Linux containers, build from the same published `dist`: `docker build -t claims .` and `docker run --rm -p 127.0.0.1:8080:8080 claims`. The smoke test exercises frontend asset packaging, the HTTP contract, deductibles, limits, late reporting, and rejected inputs without external dependencies.

## Build, deploy and roll back

1. **[Build claims](.github/workflows/build.yml)** runs on pull requests and pushes to `main`. It compiles and exercises the Windows binary, packages that exact binary, and tests the Linux image as a randomly assigned-style non-root UID. Trusted `main` builds publish the tested image to private Azure Container Registry (ACR) and attach `release.json` with its digest.
2. **[Deploy claims to OpenShift](.github/workflows/deploy.yml)** is manual. Run `gh workflow run deploy.yml --ref main -f build_run=<successful-main-build-run-id>`. It verifies the run's provenance and promotes its digest to `claims:release` without rebuilding. An OpenShift ImageStream periodically imports the tag; its image-change trigger updates the Deployment. Allow time for the scheduled import. The workflow waits until the public HTTPS route serves the exact build ID, then submits real HTTP requests. A green build alone does **not** establish a successful deploy.
3. To roll back, dispatch the same deploy workflow with a previous successful build run while its release artifact and registry digest are retained. Protect the `main` branch and review workflow changes before using this outside a disposable demo. GitHub has registry access, not cluster credentials.

## Bootstrap a fresh environment

Prerequisites: a Bash shell for the following examples, PowerShell 7.4+ for the bootstrap script, signed-in `az` and `gh`, a compatible `oc` on PATH, Actions enabled, and authority to provision ARO, assign Azure roles, and set repository variables. Use a **dedicated** resource group, registry and cluster. Check region availability, VM SKU restrictions and spare compute quota first. See [Microsoft's ARO CLI quickstart](https://learn.microsoft.com/azure/openshift/create-cluster) for provider registration, permissions, region sizing and optional Red Hat pull secret.

```bash
RG=example-openshift-rg
LOCATION='replace-with-supported-region'
CLUSTER=example-aro
REGISTRY='replace-with-globally-unique-name'
REPO='replace-with-owner/repo'
az group create -n "$RG" -l "$LOCATION"
az network vnet create -g "$RG" -n example-vnet --address-prefixes 10.70.0.0/22
az network vnet subnet create -g "$RG" --vnet-name example-vnet -n master --address-prefixes 10.70.0.0/23 --private-link-service-network-policies Disabled
az network vnet subnet create -g "$RG" --vnet-name example-vnet -n worker --address-prefixes 10.70.2.0/23
az aro create -g "$RG" -n "$CLUSTER" --vnet example-vnet --master-subnet master --worker-subnet worker
az acr create -g "$RG" -n "$REGISTRY" --sku Standard --role-assignment-mode rbac --admin-enabled false
az identity create -g "$RG" -n example-ci-identity
SUBJECT_PREFIX=$(gh api "repos/$REPO/actions/oidc/customization/sub" --jq '.sub_claim_prefix') # e.g. repo:OWNER@ID/REPO@ID
test -n "$SUBJECT_PREFIX" || { echo 'Determine the OIDC subject prefix for this repository before proceeding'; exit 1; }
az identity federated-credential create -g "$RG" --identity-name example-ci-identity -n github-main \
  --issuer https://token.actions.githubusercontent.com --subject "$SUBJECT_PREFIX:ref:refs/heads/main" --audiences api://AzureADTokenExchange
PRINCIPAL=$(az identity show -g "$RG" -n example-ci-identity --query principalId -o tsv)
SCOPE=$(az acr show -g "$RG" -n "$REGISTRY" --query id -o tsv)
for role in Reader AcrPush; do
  az role assignment create --assignee-object-id "$PRINCIPAL" --assignee-principal-type ServicePrincipal --role "$role" --scope "$SCOPE"
done
gh variable set AZURE_CLIENT_ID -R "$REPO" --body "$(az identity show -g "$RG" -n example-ci-identity --query clientId -o tsv)"
gh variable set AZURE_TENANT_ID -R "$REPO" --body "$(az account show --query tenantId -o tsv)"
gh variable set AZURE_SUBSCRIPTION_ID -R "$REPO" --body "$(az account show --query id -o tsv)"
gh variable set ACR_NAME -R "$REPO" --body "$REGISTRY"
```

Run this from the checkout after the cluster is ready:

```powershell
.\scripts\connect-project.ps1 -ResourceGroup <resource-group> -Cluster <cluster> -Registry <registry> -Repository <owner/repo>
```

For the **hosted control plane** alternative, use Microsoft's [custom hosted cluster template](https://learn.microsoft.com/azure/openshift/howto-create-custom-hosted-cluster) in a supported region, and pass `-HostedControlPlane` to the script. Pass `-RegistryResourceGroup` if the registry lives in a different group. The hosted template supports `privateKeyVault=true`; confirm that the subscription's policies and VM-family quotas permit your chosen configuration. Do not exempt shared resources from security policy to make a demo work.

The script creates the `insurance-claims` project resources, links a **read-only, claims-repository-scoped** registry token, and sets `CLAIMS_URL` in GitHub. Rerun it before the token's 90-day expiry to rotate the pull credential. On a fresh cluster the application will not be ready until the first image promotion. Reapply the manifest via the script for platform configuration changes. For rollout diagnostics, run `oc -n insurance-claims describe is claims` and `oc -n insurance-claims rollout status deploy/claims`.

To retire a dedicated installation, first record its cluster service principal (`az aro show -g "$RG" -n "$CLUSTER" --query servicePrincipalProfile.clientId -o tsv`). Delete the cluster with `az aro delete -g "$RG" -n "$CLUSTER" --yes`, then delete the dedicated resource group. Remove the dedicated app registration if still present, and remove the five repository Actions variables. Do not delete a shared resource group or identity.
