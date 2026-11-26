---
name: modernize-to-aks
description: Discover a repository's deployable workload and plan or execute its migration to AKS using its own build, platform, delivery and verification conventions.
---

# Modernize a workload to AKS

## Build a repository profile first

The profile is a short handoff in the plan, not a required configuration file. Read the consuming repository's instructions, tracked files, README, CI definitions and deployment configuration. `git ls-files` and current CI jobs are useful starting points even when no GitHub remote or cloud login is available. For GitHub, `gh repo view --json nameWithOwner,defaultBranchRef` can confirm the repository and base branch. Don't assume that the checkout branch is the release branch.

| Record | Where to find it |
| --- | --- |
| Workload and scope | Deployable project(s), owner, shared dependencies, working directories; distinguish a library from its runnable host. In a monorepo, confirm the intended workload if ambiguous. |
| Build and runtime | CI commands, project/solution or package manifest, SDK policy, tests, Dockerfile/buildpack, context, architecture and writable paths. |
| Delivery | CI provider, base and release branches, protected environment, reusable workflow or pipeline interface, manifest/Helm/Kustomize/GitOps location (possibly a separate repository). |
| Platform | Approved subscription, resource group, region, cluster, namespace, registry, identity and owners. Mark **existing**, **new/approved**, or **unknown**; CLI defaults are not approval. |
| Acceptance and rollback | Workload kind, readiness signal, release/digest evidence, representative safe smoke check, where it runs, and the owner and method for restoring the prior release. |

Record file paths or commands behind the values. Leave unresolved items explicitly unknown. Ask only about choices that affect scope, permission, cost or acceptance. Don't ask for credentials, and don't fetch from the agents' source repository. Template order: use the target's assets first, then an owner-approved source at a reviewed immutable revision; otherwise author the missing local assets.

## Use the profile to run the migration

| Stage | Action and exit evidence |
| --- | --- |
| Assess | Establish current behavior, compatibility risks and proposed resources/cost. Agree on scope. For an assessment-only request, stop here. |
| Prepare changes | Work on the designated non-default branch (create one if needed). Keep the current deployment recoverable. |
| Upgrade and package | For .NET use `dotnet-latest-upgrade`; for other components retain the native build and tests. Record actual test and container smoke results. |
| Prepare AKS | Use `aks-bicep-platform` and `aks-cluster-bootstrap`. On managed/shared infrastructure, verifying access and handing gaps to its owner may be the whole phase. |
| Wire delivery | Use `reusable-aks-pipelines`. Open/update the PR; require unprivileged PR validation. Do not merge or bypass approvals to obtain a deployment. |
| Release and verify | When authorized, deploy the tested digest and use `aks-verify-and-rollback`. Capture the run, observed identity and representative live result. If deployment must wait for merge, leave it pending. |
| Document and hand off | Update the target's README/runbook with actual commands, resource groups/resource links, endpoint or trigger, rollback and retained costs. Only decommission the prior platform with separate owner approval. |

Use current Microsoft Learn guidance for Azure and .NET; separate documented requirements from design choices. Local tests, mocks and cached output are not deployment evidence. Return the evidence obtained, the outstanding gates and who owns them.
