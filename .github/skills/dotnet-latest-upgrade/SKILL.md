---
name: dotnet-latest-upgrade
description: Assess, upgrade, test and containerize the selected .NET project without assuming a solution layout, application type, test framework or HTTP endpoint.
---

# Upgrade the selected .NET workload

1. **Locate the build boundary.** Read the `modernize-to-aks` profile or derive it from CI, project references, solution files, `global.json`, shared MSBuild props/targets and central package management. A Docker build context may be above the application directory to include shared projects. Don't upgrade every project in a monorepo by default.
2. **Establish the contract.** Run the existing test command from its expected directory. Capture representative behavior: routes and error responses for a web service, message handling and retries for a worker, input/output and exit status for a batch job. Identify Windows-only APIs, native libraries, persistence or globalization constraints before choosing a runtime image.
3. **Choose the target.** Check Microsoft's [.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) and compare the latest supported release with the repository's SDK policy and CI availability (`dotnet --list-sdks`). Upgrade in place where possible. Only change application architecture or test framework for a specific compatibility need. If the requested target cannot run a required dependency, surface the tradeoff.
4. **Build and test.** Align target frameworks, SDK pin, dependencies, CI and container runtime. Reuse the repository's runner and tests; add regression coverage for changed behavior. For a conventional .NET build the equivalents are `dotnet test <test-project-or-solution>` and `dotnet publish <deployable-project> -c Release`. Verify dependent projects affected by a shared file change.
5. **Exercise the packaged artifact.** Use a supported minimal runtime image compatible with the workload; test non-root execution, a read-only root filesystem and any explicit writable mounts. Keep existing probe paths/ports or use the workload's native readiness signal. Record a release identifier tied to the immutable image. Don't add a public endpoint just to identify a worker.

Return the exact commands and working directories, test results, image build context and entry point, and the smoke check to run after deployment. If the local host cannot build the target image, use a real CI run and report that distinction.
