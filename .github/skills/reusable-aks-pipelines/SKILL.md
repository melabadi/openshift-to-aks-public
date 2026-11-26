---
name: reusable-aks-pipelines
description: Connect a target repository to reusable build and AKS release pipelines, preserving its workload layout, branch protections and deployment tooling.
---

# Wire reusable delivery

Read the delivery fields of the `modernize-to-aks` profile and the consuming repository's CI and environment policies. Identify who applies the release: CI, a platform pipeline or a GitOps controller. Do not introduce a second writer.

**Find or define the interface.** Prefer compatible workflows already in the repository. An approved central GitHub workflow can be called as `OWNER/REPO/.github/workflows/FILE.yml@FULL_COMMIT_SHA`; confirm the caller has access. For a local workflow, use `./.github/workflows/FILE.yml`. Read `on.workflow_call.inputs`, required permissions and outputs at that revision. If there is no suitable source, implement a local reusable workflow; these agents do not require access to an external template. For other CI providers, map the same contracts to their native reusable templates.

| Stage | Bind from the repository profile | Output or gate |
| --- | --- | --- |
| Build/test | Workload directory, SDK/toolchain, project or package, test command, image context, entry point and smoke command | Passing tests and a smoke-tested artifact. PR jobs have no cloud write credentials. |
| Publish | Approved release event, environment, registry login server and federated publisher | Push the tested artifact; return `registry/repository@sha256:…` and a release identifier tied to the commit. |
| Deploy | Environment, cluster/namespace, owning manifest/overlay/chart/values or GitOps repo, image digest | Render and validate the package; reject unresolved placeholders. Apply through the owner or commit the desired digest for reconciliation. |
| Verify and roll back | Workload resource/container selector, real readiness and smoke checks, previous approved digest/config | Invoke `aks-verify-and-rollback` from a network that can reach the workload. |

Use the actual release branch and protection rules. On GitHub, keep PR validation separate from environment-gated publish/deploy; grant `id-token: write` only to the jobs that need federation, and pin external actions/workflows by full SHA. Set non-secret variables under the names read by the caller. Do not weaken environment protection to get a first run: `workflow_dispatch` requires the workflow to exist on the default branch; leave the deploy pending if that gate is not met.

Check the rendered workload for non-root operation, a read-only filesystem where compatible, resource limits and workload-appropriate probes. Only create Service, routing and TLS resources for workloads that need them. Serialize releases to the same workload/environment. Return the actual workflow references, run URL, release and digest rather than filenames from an example.
