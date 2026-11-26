---
name: aks-verify-and-rollback
description: Trace a release from CI to a running AKS workload, verify representative behavior at the intended access point and identify or execute an authorized rollback.
---

# Verify a release and its recovery path

Use the commit, environment, namespace, workload/container selector, expected digest, smoke check and rollback owner from the `modernize-to-aks` profile. Independently confirm the deployment target. If the request is review-only, observe and report; do not trigger a deploy, write production data or run a rollback.

1. **Trace the artifact.** Find the successful release job for the intended commit/environment (not just the last green build). Record the run URL, digest and release ID. For GitOps, also record the reconciled revision. Inspect the workload template and running pod container status; match the selected container, not the first container in a pod. With multi-platform images, the runtime may report a platform-specific child digest; verify it belongs to the published index.
2. **Check readiness for the actual kind.** Examples: `kubectl rollout status deployment/<name> -n <namespace> --timeout=5m` for a Deployment; check Job completion and exit status for a batch workload. For routed services, check the applicable route and TLS status. Do not assume every workload uses a Gateway, a public hostname or a certificate.
3. **Prove behavior.** Run the agreed bounded smoke check from the intended vantage point. For a public API, validate TLS, expected response and the release if it exposes one. For a private service, use an authorized in-network runner. For a worker, observe a safely attributable processed event or completed job and its expected output. Do not expose a private workload for testing or inject production work without approval. If evidence is unavailable, mark verification blocked; never substitute a local or mock result.
4. **Review recovery before acting.** Identify a known-good earlier digest, corresponding configuration and compatibility with current data/schema. Specify the repository's actual recovery command or GitOps revert and the authorized operator. A procedure reviewed is not a rollback tested. Execute only on explicit authorization; promote the prior immutable artifact rather than rebuilding a tag, then repeat steps 1-3 for that release.

Report each check with the expected and observed result, time and supporting run/resource link. If a check fails, distinguish artifact mismatch, scheduling/image pull, failed reconciliation, routing/TLS, or application behavior. A PR-only check cannot establish live success.
