---
name: copilot-fusion-routing
description: Run the modernization agents on Copilot Fusion - which Fusion tier each agent uses, how agent frontmatter pins it (hydrafusion, hydrafusion-max, model-policy required), how to select Fusion in the CLI and app, and what managed policy can override. Use when launching, adapting or troubleshooting the modernization agents' model selection.
---

# Copilot Fusion for the modernization agents

Copilot Fusion is a Copilot routing mode: instead of one model per request, Fusion routes work across multiple models at a chosen tier. Tiers are `standard`, `high` and `max`.

## Tier map used by these agents

| Agent | Role | Frontmatter `model` | Why |
| --- | --- | --- | --- |
| `aks-modernization-lead` | plan, delegate, integrate | `[hydrafusion-max, hydrafusion]` | long-horizon planning and judgment |
| `modernization-verifier` | independent review and live verification | `[hydrafusion-max, hydrafusion]` | review quality matters most |
| `dotnet-modernizer` | app upgrade and container | `[hydrafusion, hydrafusion-max]` | implementation at High, Max as fallback |
| `aks-platform-engineer` | Bicep, AKS, bootstrap | `[hydrafusion, hydrafusion-max]` | same |
| `delivery-pipeline-engineer` | workflows, manifests, first deploy | `[hydrafusion, hydrafusion-max]` | same |

Every agent also sets `model-policy: required`, so the frontmatter list is not silently replaced.

## How the pinning works (verified against Copilot CLI 1.0.114)

- Agent `model` accepts an ordered list; the first available entry is used.
- `hydrafusion` (High) and `hydrafusion-max` (Max) are the Fusion model ids accepted in agent frontmatter. The word `fusion` is the CLI/SDK *model name* but is **rejected** as an agent `model` value: the agent is deselected with "None of the agent's preferred models are available".
- `model-policy: required` keeps the list authoritative. Without it the default `preferred` lets users and sessions override the agent's model.
- The `task` tool's `model` parameter lists `hydrafusion` and `hydrafusion-max`, so the lead can delegate at an explicit tier when it must.

## Launch

CLI (interactive or one-shot):

```powershell
copilot --agent aks-modernization-lead --model fusion
copilot --agent aks-modernization-lead --model fusion -p "Use modernize-to-aks to assess this checkout. Identify the workloads and existing delivery path; do not edit or deploy."
```

GitHub Copilot app: pick **Copilot Fusion** in the model picker, then choose the `aks-modernization-lead` agent.

## Managed policy and availability

- An administrator-managed setting can pin the session baseline (for example `auto` with `fusionTier: high`); the CLI then prints that it is using the managed model. In the verified run the agents still switched the Fusion tier on selection (`Model changed from Copilot Fusion (High) to HydraFusion Max` for the Max agents, `… to HydraFusion` for the High agents). A stricter policy could still override the agent's `model`; treat the frontmatter as authoritative only when the CLI log shows the change.
- If Fusion is **not** available to a user, a `required` agent is deselected and the default agent runs. To allow a fallback, remove `model-policy: required` and append a model the user has, for example `model: [hydrafusion, <model-id-from-/model>]`. Use ids the user's `/model` list shows.
- Acceptance of the ids was verified; which underlying models Fusion routes to is internal to Copilot and not observable from the agent files.

## Per-user overrides

User config can set per-subagent preferences under `subagents.agents.<agent-name>` (`model`, `effortLevel`, `contextTier`; each can be `inherit`). Prefer leaving them at `inherit` so frontmatter wins.

## Adopting the agents elsewhere

- Repository: copy `.github/agents` and `.github/skills`.
- User: copy to `~/.copilot/agents` and `~/.copilot/skills`.
- Organization: put the agent files in `agents/` in the organization's `.github-private` repository; make the skills available separately at repository or user scope.

Start in the target checkout. Neither model routing nor skill discovery needs access to the repository where the agents were authored. If Fusion is required and the CLI deselects the agent, stop rather than continuing with the default agent.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| "None of the agent's preferred models are available; deselected to the default agent" | The account lacks Fusion, or a value is not a model id. Check `/model`; relax `model-policy` and add a fallback. |
| Agent not found | The file must be `.github/agents/<name>.agent.md` on the branch you are running from; names use letters, digits, `.`, `-`, `_`. |
| Skill not found | `.github/skills/<name>/SKILL.md` with `name` and `description` frontmatter; restart the session after adding. |
| Model is not the one in frontmatter | A managed policy or an explicit `--model` flag overrides `preferred` selection. |
