# azdo — Command Reference

> **Version**: 1.3.4 | **Package**: `AzDo` | **MCP Tools**: 69

All commands support `--org` and `--project` overrides (optional — auto-detected from git remote, or `.azdo` file if not in a git repo).

All **read** commands support `--table` to render output as an ASCII table instead of JSON (overrides `--compact`). In MCP mode, pass `"table": true`.

---

## 📋 CLI Commands

### Context
| Command | Description |
|---|---|
| `azdo context` | Detect Azure DevOps org/project/repo from the current git remote (or `.azdo` file). Shows whether the detected org is registered. Use to verify what `azdo` sees before running other commands. |

> **`.azdo` file**: If not in a git repo, `azdo` searches parent directories for a `.azdo` JSON file:
> ```json
> { "org": "myorg", "project": "myproject" }
> ```
> - `org` is required; `project` is optional.
> - When inside a git repository, the `.azdo` file is **ignored** — git remote always wins.

### Configuration
| Command | Description |
|---|---|
| `azdo config list` | List all configured organizations with masked PATs. Shows org name and URL. Use to verify which orgs are registered before working across multiple tenants. |
| `azdo config add --org X --pat <token>` | Register an organization with a Personal Access Token (PAT). The PAT is stored in `~/.config/devops-cli/config.json` (`%APPDATA%\devops-cli\config.json` on Windows) with permissions `600`, written atomically (temp file locked down *before* the atomic replace). Required PAT scopes: `vso.work`, `vso.code`, `vso.build`, `vso.graph`, `vso.memberentitlementmanagement`. |
| `azdo config remove --org X` | Remove an organization and its stored PAT from local config. Safe to run — only deletes the local credential entry. |
| `azdo config path` | Print the full path to the config file on disk. Useful for scripting or manual inspection. |
| `azdo config validate-pats` (alias `check-pats`) | Validate every saved PAT against Azure DevOps (`GET _apis/connectionData`, per-request Basic auth). Prints a table with organization, URL, status (Attivo/Invalido/Mancante/Errore), and masked PAT. Exits `0` when all PATs are active, `1` when at least one is invalid or missing; a temporary network error is reported per-org without failing the whole run. Note: PAT expiration is not available via the API, as it does not accept PAT authentication. |

### Work Items
| Command | Description |
|---|---|
| `azdo wi query --wiql "SELECT ..." [--top 10] [--compact] [--table]` | Run a WIQL (Work Item Query Language) query against Azure DevOps. Returns matching work items with id, type, state, title, assigned-to, and changed-date. Standard ADO WIQL syntax. `--table` renders a compact terminal-friendly table. |
| `azdo wi get --ids 1,2,3 [--fields System.Title,System.State] [--compact] [--table]` | Fetch one or more work items by comma-separated numeric IDs. Use `--fields` to limit returned fields (e.g. `System.Title,System.AssignedTo`). |
| `azdo wi create --type Task --title "..." [--description "..."] [--assigned-to me@eng.it] [--parent 1234] [--iteration "MyProject\\Sprint 1"]` | Create a new work item (Task, Bug, User Story, Feature, Epic). Optionally link to a parent work item, assign to a team member (by email), and place in an iteration (full path with escaped `\\`). |
| `azdo wi update --id 1234 [--state Active] [--title "..."] [--assigned-to me@eng.it]` | Update an existing work item's state, title, or assignee. Only the fields you provide are changed — partial update. |
| `azdo wi comment --id 1234 --text "..."` | Add a text comment to a work item. Comments appear in the Discussion section of the work item in the Azure DevOps UI. |
| `azdo wi hierarchy --id 1234 [--table]` | Get a work item with its full child tree (recursive parent → children → grandchildren). With `--table`, children are indented visually. |
| `azdo wi link --source 1234 --target 5678 --type child` | Link two work items. Link types: `child` (source becomes parent of target), `parent` (source becomes child of target), `related`, `duplicate`. |
| `azdo wi search --query "keyword" [--type Bug] [--top 10] [--table]` | Full-text search across all work items. Matches title, description, and other text fields. Requires Azure DevOps search indexing to be enabled in the organization. |

### Projects
| Command | Description |
|---|---|
| `azdo project list [--top 20] [--skip 10] [--compact] [--table]` | List all projects in the organization with id, name, state, visibility, and last-update timestamp. Use `--top`/`--skip` for pagination. |
| `azdo project get --id <name-or-id> [--compact] [--table]` | Get detailed information about a single project by its UUID or name. Returns full metadata including URL, description, visibility, and revision. |
| `azdo project clone-all [--output <dir>] [--ssh] [--dry-run] [--parallel 4]` | Clone (or pull if already cloned) every repository from every project in the organization. Outputs to `org/project/repo` folder structure. `--parallel 4` speeds up large orgs. `--dry-run` previews without cloning. |

### Boards — Iterations, Teams, Areas
| Command | Description |
|---|---|
| `azdo board iterations [--team X] [--compact] [--table]` | List all iterations (sprints) for a project, optionally filtered by team. Shows name, path, start/finish dates, and timeframe. |
| `azdo board iteration current [--team X] [--compact] [--table]` | Get the currently active sprint for a team. Returns the single iteration that contains today's date. Useful for CI/CD scripts. |
| `azdo board teams [--compact] [--table]` | List all teams in the project with id, name, and description. Team names can be used as `--team` in other board commands. |
| `azdo board members --team-id X [--compact] [--table]` | List all members of a specific team with display name, unique name, and identity id. `--team-id` accepts both UUID and team name. |
| `azdo board areas [--depth 5] [--compact] [--table]` | List area paths as a flattened tree. With `--table`, depth is shown via indentation. Use `--depth` to control nesting level (default 5). |
| `azdo board backlog [--team X] [--compact] [--table]` | Get the product backlog for a team. Shows backlog levels (Epics, Features, Stories) with name, type, and rank. This retrieves the backlog configuration, not individual work items. |
| `azdo board sprint --iteration X [--team X] [--table]` | Get the sprint backlog — work items assigned to a specific iteration. `--iteration` is required (name or full path like `"MyProject\\Sprint 1"`). |
| `azdo board iteration create --name "Sprint 42" [--start 2026-01-01] [--finish 2026-01-14]` | Create a new sprint/iteration in the project. Dates use ISO 8601 format. |
| `azdo board area create --name "NewArea" [--parent "ParentArea"]` | Create a new area path, optionally nested under a parent area. Parent uses the short name, not the full path. |
| `azdo board team create --name "MyTeam" [--description "..."]` | Create a new team in the project. After creating, use `set-area` to assign it an area path. |
| `azdo board team set-area --team-id X --area-path "Project\\Area"` | Assign an area path to a team. Area path must be the full path including project root. |
| `azdo board team add-member --team-id X --member-id <guid>` | Add a user to a team. `--member-id` must be the Azure DevOps identity GUID — use `azdo identity search` to find it. |
| `azdo board team remove-member --team-id X --member-id <guid>` | Remove a user from a team by their identity GUID. Does not remove the user from the org — only from the team. |

### Identity, Users & Groups
| Command | Description |
|---|---|
| `azdo identity search --query "email@example.com" [--top 20] [--compact] [--table]` | Search Azure DevOps identities by email or display name via the Graph API. Returns descriptor (e.g. `aad.…`), display name, email, principal name, origin, and subject kind. Use to find GUIDs needed for team/group operations. Requires `vso.graph` PAT scope. |
| `azdo identity list-users [--top 100] [--skip 0] [--compact] [--table]` | List all users in the organization with entitlements (license type, status). Uses the Member Entitlement Management API. Requires `vso.memberentitlementmanagement` PAT scope. |
| `azdo identity add-user --email user@example.com [--license express]` | Invite a new user to the Azure DevOps organization. Default license is `express` (free Basic). Options: `express`, `stakeholder`, `basic`, `advanced`, `basicAndTestPlans`. User receives an invitation email. |
| `azdo identity remove-user --id <guid>` | Remove a user from the organization by their entitlement ID (GUID from `list-users`). Permanent — the user loses access to all projects. |
| `azdo identity list-groups [--compact] [--table]` | List all groups in the organization. Shows display name, description, origin, scope type, and descriptor (e.g. `vssgp.…`). Use descriptors in group member operations. |
| `azdo identity group-members --descriptor vssgp.… [--compact] [--table]` | List all members of a group by its Graph descriptor. Members can be users or nested groups. Member names are resolved automatically. |
| `azdo identity group-create --name "MyGroup" [--description "..."]` | Create a new group. If `--project` is provided (or auto-detected), creates a project-scoped group; otherwise org-wide. |
| `azdo identity group-member-add --group vssgp.… --member aad.…` | Add a user or group to a group. Both identified by their Graph descriptors. |
| `azdo identity group-member-remove --group vssgp.… --member aad.…` | Remove a member from a group by descriptor. Does not delete the member — only removes membership. |

> **Note**: Identity and group tools use the Azure DevOps **Graph API** (`vssps.dev.azure.com`). User entitlement tools use the **Member Entitlement Management API** (`vsaex.dev.azure.com`). These require `vso.graph` and `vso.memberentitlementmanagement` PAT scopes respectively.

### Repositories & Git
| Command | Description |
|---|---|
| `azdo repo list [--top 20] [--skip 10] [--compact] [--table]` | List all repositories in the project with id, name, and default branch. Supports pagination. |
| `azdo repo create --name "MyNewRepo"` | Create a new empty Git repository in the project. Clone the returned SSH or HTTPS URL to start working. |
| `azdo repo branches --repo X [--search feature] [--top 20] [--compact] [--table]` | List branches in a repository. `--search` filters by name (contains). Returns branch name, abbreviated object ID, and creator. |
| `azdo repo branch create --repo X --name feature/foo --from main` | Create a new branch from a source ref (branch name or commit SHA). `--from` accepts both `main` and `refs/heads/main`. |
| `azdo repo branch delete --repo X --name feature/foo` | Delete a branch from the remote repository. Your local clone is not affected. |
| `azdo repo commits --repo X [--branch main] [--author ...] [--since 2026-01-01] [--until 2026-06-01] [--top 50] [--skip 0] [--compact] [--table]` | List commits with full filtering: branch, author email, date range (ISO 8601), and pagination. Each commit shows abbreviated SHA, author, email, date, and message. |
| `azdo repo diff --repo X --base main --target feature/foo [--table]` | Get the diff (changed files) between two commits or branches. With `--table`, shows a compact CHANGE \| PATH table. Without it, returns the full REST API response. |
| `azdo repo file --repo X --path src/Program.cs [--ref main] [--table]` | Fetch a single file's content from a repository at a specific branch or commit. Returns path, commit ID, encoding, and content. `--ref` defaults to the default branch. |
| `azdo repo file push --repo X --path src/foo.cs --content "..." --branch main --message "Add foo.cs"` | Create or update a file via a direct commit to a branch. No local clone needed. Ideal for CI/CD pipelines that need to commit generated files. |
| `azdo repo file delete --repo X --path src/foo.cs --branch main --message "Remove foo.cs"` | Delete a file via a direct commit. The file is removed immediately; commit history is preserved. |
| `azdo repo tags --repo X [--compact] [--table]` | List annotated and lightweight tags in a repository. Shows tag name (without `refs/tags/` prefix), abbreviated object ID, and creator. |
| `azdo repo tag create --repo X --name v1.0 --message "Release v1.0" --object-id <sha>` | Create an annotated tag pointing to a specific commit. `--object-id` is the full commit SHA. |
| `azdo repo clone-all [--output <dir>] [--ssh] [--dry-run] [--parallel 4]` | Clone (or pull if already cloned) all repositories in the project. Scoped to the current project. Use `--ssh` for SSH URLs. |

### Pull Requests
| Command | Description |
|---|---|
| `azdo pr list [--repo X] [--status active] [--creator me@eng.it] [--top 20] [--skip 0] [--compact] [--table]` | List pull requests with optional filters. Status: `active`, `abandoned`, `completed`, `all`. Shows id, title, status, draft flag, creator, date, and source→target branches. Without `--repo`, lists across the whole project. |
| `azdo pr create --title "..." [--repo X] [--source feature/foo] [--target main] [--description "..."] [--draft] [--work-item 1234] [--reviewer <guid>]` | Create a new pull request. Defaults: source = current git branch, target = `main`, repo = detected from git remote. Supports draft mode, work item linking, and reviewer assignment (reviewers must be identity GUIDs — use `azdo identity search`). |
| `azdo pr vote --pr 1234 [--repo X] --voter-id <guid> --vote 10` | Vote on a pull request. Vote values: `10`=approve, `5`=approve with suggestions, `0`=reset, `-5`=waiting, `-10`=reject. Find your `--voter-id` GUID in your ADO profile URL. |
| `azdo pr abandon --pr 1234 [--repo X]` | Abandon a pull request. Equivalent to closing without merging. |
| `azdo pr complete --pr 1234 [--repo X] [--delete-source]` | Complete (merge) a pull request. `--delete-source` removes the source branch after merging. PR must be mergeable (no conflicts, policies satisfied). |
| `azdo pr detail --pr 1234 [--repo X] [--table]` | Get full PR details: title, status, creator, branches, merge status, reviewer votes (decoded: `10`=approved, `-10`=rejected), and labels. With `--table`, reviewers are shown as a nested table. |
| `azdo pr threads --pr 1234 [--repo X] [--compact] [--table]` | List all review threads on a PR: inline comments, file paths, line numbers, authors, and thread status (active/resolved). Shows the first comment of each thread. |
| `azdo pr comment --pr 1234 --text "..." [--repo X] [--file src/Foo.cs --line 42]` | Add a comment to a pull request. Without `--file`/`--line`, creates a top-level thread. With file/line, creates an inline comment on the code diff. |

### Builds, Pipelines & Releases
| Command | Description |
|---|---|
| `azdo build list [--top 20] [--definition 5] [--compact] [--table]` | List recent builds. Filter by pipeline definition ID. Shows build number, pipeline name, status, result, branch, queue time, and finish time. `--table` renders perfect CI dashboard tables. |
| `azdo build status --id 1234 [--compact] [--table]` | Get detailed build status with all timeline records (tasks, phases). With `--table`, shows a vertical info block + TIMELINE sub-table with each task's state, result, and timing. |
| `azdo build trigger --definition 5 [--branch refs/heads/main]` | Trigger a new pipeline run. `--branch` defaults to `refs/heads/main`. |
| `azdo build logs --id 1234 [--compact] [--table]` | List task-level log URLs for a build. Each log has an id, type, and URL to the raw log content on Azure DevOps blob storage. |
| `azdo pipeline list [--top 20] [--compact] [--table]` | List all YAML and classic pipelines with id, name, folder, and revision. The id is the `--definition` value for `build list` and `build trigger`. |
| `azdo release definitions [--search X] [--top 20] [--compact] [--table]` | List release definitions with optional name search. Shows id, name, path, creator, and creation date. |
| `azdo release list [--definition 5] [--status active] [--top 20] [--compact] [--table]` | List releases, optionally filtered by definition and status (`active`/`completed`). Shows environments with their deployment statuses inline. |
| `azdo release trigger --definition 5 [--description "..."]` | Trigger a new release from a release definition. The description appears in the release history. |

### Wiki
| Command | Description |
|---|---|
| `azdo wiki list [--compact] [--table]` | List all wikis in the project. Shows id, name, type (project/code), and version. The id or name is used in other wiki commands. |
| `azdo wiki page --wiki-id X --path "/Architecture/Overview" [--compact] [--table]` | Fetch a wiki page by path. Returns page content (Markdown), path, title, and URL. Path must match exactly. |
| `azdo wiki upsert --wiki-id X --path "/Guide" --content "# Hello"` | Create or update a wiki page. Content must be valid Markdown. Path must start with `/`. Creates parent pages automatically. |

### Search
| Command | Description |
|---|---|
| `azdo search code --query "IDisposable" [--repo X] [--top 20] [--table]` | Full-text search across all code files (or scoped to a repo). Results show file name, path, repository, and match count. Requires Azure DevOps code search indexing to be enabled. |
| `azdo search wi --query "authentication" [--type Bug] [--top 20] [--table]` | Full-text search across work items. Optionally filter by type. Matches across title, description, and text fields. Requires work item search indexing. |

### MCP Server
| Command | Description |
|---|---|
| `azdo mcp serve [--transport stdio|http] [--port 9287]` | Start the MCP server. `stdio` for Claude Desktop/Copilot/Cursor (default). `http` transport with SSE for browser-based clients on the specified port (binds loopback only; cross-origin browser requests are rejected with `403` to block DNS-rebinding). All 69 tools become available to your AI agent. |
| `azdo mcp list-tools` | List all 69 MCP tools as a JSON array with name, description, and input schema. Output is the raw `tools/list` MCP response. |

---

## 🔌 MCP Tools (68 total)

Use these in Claude Desktop, Copilot, Cursor, or any MCP-compatible agent.

### Context & Session
| Tool | Required Args | Optional Args | Description |
|---|---|---|---|
| `azdo_context` | *(none)* | — | Detect org/project/repo from git remote. Returns detected org URL, project, repo, registration status, and session overrides. |
| `azdo_context_set` | *(none)* | `org`, `project` | Set default org/project overrides for the current session. Used when a tool call omits org/project. |

### Work Items
| Tool | Required Args | Optional Args | Description |
|---|---|---|---|
| `azdo_wi_query` | `wiql` | `top`, `compact`, `table`, `org`, `project` | Run a WIQL query against work items. Returns matching items with id, type, state, title, assigned-to, changed-date. |
| `azdo_wi_get` | `ids` | `fields`, `compact`, `table`, `org`, `project` | Get one or more work items by comma-separated IDs. `fields` limits returned system fields (e.g. `System.Title,System.State`). |
| `azdo_wi_create` | `type`, `title` | `description`, `assignedTo`, `parent`, `iteration`, `org`, `project` | Create a work item (Task, Bug, User Story, Feature, Epic). `parent` links to a parent work item. `iteration` uses the full path. |
| `azdo_wi_update` | `id` | `state`, `title`, `assignedTo`, `parent`, `iteration`, `org`, `project` | Update a work item. Only provided fields are changed. |
| `azdo_wi_comment` | `id`, `text` | `org`, `project` | Add a comment to a work item's Discussion section. |
| `azdo_wi_hierarchy` | `id` | `table`, `org`, `project` | Get work item with full child tree (recursive). With `table: true`, shows an indented tree. |
| `azdo_wi_link` | `sourceId`, `targetId`, `linkType` | `org`, `project` | Link two work items. `linkType`: `child`, `parent`, `related`, `duplicate`. |
| `azdo_wi_search` | `query` | `type`, `top`, `table`, `org`, `project` | Full-text search on work items. Filter by type. Requires search indexing enabled. |

### Projects
| Tool | Required Args | Optional Args | Description |
|---|---|---|---|
| `azdo_project_list` | *(none)* | `top`, `skip`, `compact`, `table`, `org` | List projects in the organization. |
| `azdo_project_get` | `id` | `compact`, `table`, `org` | Get project details by ID or name. |
| `azdo_project_clone_all` | *(none)* | `org`, `output`, `ssh`, `dryRun`, `parallel` | Clone (or pull) all repos from all projects to `org/project/repo` structure. |

### Boards — Iterations
| Tool | Required Args | Optional Args | Description |
|---|---|---|---|
| `azdo_iterations_list` | *(none)* | `team`, `compact`, `table`, `org`, `project` | List iterations/sprints for a project. |
| `azdo_iteration_current` | *(none)* | `team`, `compact`, `table`, `org`, `project` | Get the currently active sprint for a team. |
| `azdo_iteration_create` | `name` | `startDate`, `finishDate`, `org`, `project` | Create a new sprint/iteration. Dates in ISO 8601. |

### Boards — Teams
| Tool | Required Args | Optional Args | Description |
|---|---|---|---|
| `azdo_teams_list` | *(none)* | `compact`, `table`, `org`, `project` | List teams in a project. |
| `azdo_members_list` | `teamId` | `compact`, `table`, `org`, `project` | List members of a team by ID or name. |
| `azdo_team_create` | `name` | `description`, `org`, `project` | Create a new team. |
| `azdo_team_area_set` | `teamId`, `areaPath` | `org`, `project` | Assign an area path to a team. Area path must include project root. |
| `azdo_team_member_add` | `teamId`, `memberId` | `org`, `project` | Add a user to a team. `memberId` must be an identity GUID. |
| `azdo_team_member_remove` | `teamId`, `memberId` | `org`, `project` | Remove a user from a team by identity GUID. |

### Identity, Users & Groups
| Tool | Required Args | Optional Args | Description |
|---|---|---|---|
| `azdo_identity_search` | *(none)* | `query`, `top`, `compact`, `table`, `org` | Search identities by email or display name (Graph API). Returns descriptor, display name, email, principal name, origin, subject kind. Requires `vso.graph` scope. |
| `azdo_group_list` | *(none)* | `compact`, `table`, `org`, `project` | List groups in the organization (Graph API). Returns display name, description, origin, scope, descriptor. |
| `azdo_group_members` | `groupDescriptor` | `compact`, `table`, `org` | List members of a group by descriptor. Members can be users or nested groups. |
| `azdo_group_create` | `name` | `description`, `org`, `project` | Create a group. Project-scoped if project is provided, otherwise org-wide. |
| `azdo_group_member_add` | `groupDescriptor`, `memberDescriptor` | `org` | Add a user or group to a group. Both by descriptor. |
| `azdo_group_member_remove` | `groupDescriptor`, `memberDescriptor` | `org` | Remove a member from a group by descriptor. |
| `azdo_user_list` | *(none)* | `top`, `skip`, `compact`, `table`, `org` | List users with entitlements (MEM API). Shows license and status. Requires `vso.memberentitlementmanagement` scope. |
| `azdo_user_add` | `email` | `license`, `org` | Invite a user to the organization. License: `express` (default), `stakeholder`, `basic`, `advanced`, `basicAndTestPlans`. |
| `azdo_user_remove` | `userId` | `org` | Remove a user from the organization by entitlement ID (GUID from `user_list`). |

> **Graph descriptors**: Group and user descriptors are opaque identifiers like `vssgp.…` (group) or `aad.…` (AAD user). Use `azdo_identity_search` to find user descriptors and `azdo_group_list` to find group descriptors. `azdo_group_members` automatically resolves member names via batch subject lookup.

### Boards — Areas & Backlogs
| Tool | Required Args | Optional Args | Description |
|---|---|---|---|
| `azdo_areas_list` | *(none)* | `depth`, `compact`, `table`, `org`, `project` | List area paths as a flattened tree. `depth` controls max nesting level. |
| `azdo_area_create` | `name` | `parentPath`, `org`, `project` | Create a new area path, optionally nested under a parent. |
| `azdo_backlog_product` | *(none)* | `team`, `compact`, `table`, `org`, `project` | Get product backlog configuration (levels: Epics, Features, Stories). |
| `azdo_backlog_sprint` | `iteration` | `team`, `table`, `org`, `project` | Get sprint backlog — work items in a specific iteration. Returns item IDs and URLs. |

### Repositories — Read
| Tool | Required Args | Optional Args | Description |
|---|---|---|---|
| `azdo_repo_list` | *(none)* | `top`, `skip`, `compact`, `table`, `org`, `project` | List repositories in the project. |
| `azdo_git_branches` | `repo` | `search`, `top`, `compact`, `table`, `org`, `project` | List branches. `search` filters by name (contains). |
| `azdo_git_commits` | `repo` | `branch`, `author`, `from`, `to`, `top`, `skip`, `compact`, `table`, `org`, `project` | List commits with full filtering by branch, author, date range, and pagination. |
| `azdo_git_diff` | `repo`, `base`, `target` | `table`, `org`, `project` | Get diff between two commits/branches. With `table:true`, shows CHANGE \| PATH. |
| `azdo_git_file` | `repo`, `path` | `ref`, `table`, `org`, `project` | Get file content. `ref` defaults to the default branch. |
| `azdo_repo_tags_list` | `repo` | `compact`, `table`, `org`, `project` | List annotated and lightweight tags. |

### Repositories — Write
| Tool | Required Args | Optional Args | Description |
|---|---|---|---|
| `azdo_repo_create` | `name` | `org`, `project` | Create a new empty repository. |
| `azdo_repo_branch_create` | `repo`, `branchName`, `sourceRef` | `org`, `project` | Create a branch from a source ref (branch name or commit SHA). |
| `azdo_repo_branch_delete` | `repo`, `branchName` | `org`, `project` | Delete a branch from the remote repository. |
| `azdo_repo_tag_create` | `repo`, `tagName`, `message`, `objectId` | `org`, `project` | Create an annotated tag pointing to a commit SHA. |
| `azdo_repo_file_push` | `repo`, `path`, `content`, `branch`, `commitMessage` | `org`, `project` | Create or update a file via direct commit. No local clone needed. |
| `azdo_repo_file_delete` | `repo`, `path`, `branch`, `commitMessage` | `org`, `project` | Delete a file via direct commit. |
| `azdo_repo_clone_all` | *(none)* | `org`, `project`, `output`, `ssh`, `dryRun`, `parallel` | Clone all repositories in the project. |

### Pull Requests
| Tool | Required Args | Optional Args | Description |
|---|---|---|---|
| `azdo_pr_list` | *(none)* | `repo`, `status`, `creator`, `top`, `skip`, `compact`, `table`, `org`, `project` | List pull requests with optional filters. Status: `active`, `abandoned`, `completed`, `all`. |
| `azdo_pr_create` | `title` | `repo`, `source`, `target`, `description`, `draft`, `workItems`, `reviewers`, `org`, `project` | Create a pull request. Defaults: source = current branch, target = `main`, repo = detected. Reviewers must be identity GUIDs. |
| `azdo_pr_vote` | `prId`, `repo`, `voterId`, `vote` | `org`, `project` | Vote on a PR: `10`=approve, `5`=approve+suggestions, `0`=reset, `-5`=waiting, `-10`=reject. |
| `azdo_pr_update` | `prId`, `repo` | `status`, `title`, `autoComplete`, `deleteSourceBranch`, `org`, `project` | Update PR status (`abandoned`/`completed`), title, or enable auto-complete. |
| `azdo_pr_detail` | `prId`, `repo` | `table`, `org`, `project` | Get PR details with reviewers, votes, labels, and merge status. Votes are decoded in table mode. |
| `azdo_pr_threads` | `prId`, `repo` | `compact`, `table`, `org`, `project` | List review threads with comments, file paths, line numbers, and status. |
| `azdo_pr_comment` | `prId`, `repo`, `content` | `threadId`, `file`, `line`, `org`, `project` | Add a comment. With `file`/`line`: inline diff comment. Without: top-level thread. |
| `azdo_pr_resolve` | `prId`, `repo`, `threadId` | `org`, `project` | Resolve a review thread (mark as closed). |

### Builds & Pipelines
| Tool | Required Args | Optional Args | Description |
|---|---|---|---|
| `azdo_build_list` | *(none)* | `top`, `definition`, `compact`, `table`, `org`, `project` | List recent builds. Filter by pipeline definition ID. |
| `azdo_build_status` | `id` | `compact`, `table`, `org`, `project` | Get build status and timeline records (tasks, phases, timing). |
| `azdo_build_trigger` | `definition` | `branch`, `org`, `project` | Trigger a pipeline run. `branch` defaults to `refs/heads/main`. |
| `azdo_pipelines_list` | *(none)* | `top`, `compact`, `table`, `org`, `project` | List YAML and classic pipelines. |
| `azdo_pipeline_create` | `name`, `repo` | `branch` (default main), `yamlPath` (default /azure-pipelines.yml), `folder`, `org`, `project` | Create a YAML pipeline from a repo file. Errors if a pipeline with the same name exists in the folder. CLI: `azdo pipeline create`. |
| `azdo_build_logs` | `buildId` | `compact`, `table`, `org`, `project` | Get task-level log URLs for a build. |

### Release Management
| Tool | Required Args | Optional Args | Description |
|---|---|---|---|
| `azdo_release_definitions` | *(none)* | `search`, `top`, `compact`, `table`, `org`, `project` | List release definitions with optional name search. |
| `azdo_release_list` | *(none)* | `definitionId`, `status`, `top`, `compact`, `table`, `org`, `project` | List releases. Filter by definition and status. Shows environments inline. |
| `azdo_release_trigger` | `definitionId` | `description`, `org`, `project` | Trigger a release from a release definition. |

### Wiki
| Tool | Required Args | Optional Args | Description |
|---|---|---|---|
| `azdo_wiki_list` | *(none)* | `compact`, `table`, `org`, `project` | List wikis in the project. |
| `azdo_wiki_page_get` | `wikiId`, `path` | `compact`, `table`, `org`, `project` | Get a wiki page by path. Returns Markdown content. |
| `azdo_wiki_page_upsert` | `wikiId`, `path`, `content` | `org`, `project` | Create or update a wiki page. Content must be Markdown. |

### Search
| Tool | Required Args | Optional Args | Description |
|---|---|---|---|
| `azdo_code_search` | `query` | `repo`, `top`, `table`, `org`, `project` | Full-text code search. Results: file name, path, repository, match count. |
| `azdo_wi_search` | `query` | `type`, `top`, `table`, `org`, `project` | Full-text work item search. Filter by type. |

---

## 🗳️ Vote Values Reference
| Vote | Meaning |
|---|---|
| `10` | Approved |
| `5` | Approved with suggestions |
| `0` | Reset vote |
| `-5` | Waiting for author |
| `-10` | Rejected |

## 🔗 Work Item Link Types
| Link Type | Description |
|---|---|
| `child` | Hierarchy-Forward (parent → child) |
| `parent` | Hierarchy-Reverse (child → parent) |
| `related` | Related work item |
| `duplicate` | Duplicate-Forward |

---

## 📝 Notes

- **`compact`**: Set to `false` to get the full raw Azure DevOps API response. Default is `true` (simplified output optimized for LLM consumption).
- **`table`**: Set to `true` to render output as a human-readable ASCII table. Default is `false`. When `table` is `true`, `compact` is ignored — table format always takes precedence. Available on all read/get/list tools (both CLI and MCP). Useful for quick visual inspection in the terminal.
- **`org` / `project`**: Auto-detected from the current git remote, or from a `.azdo` file if not in a git repo. Pass `--org` / `--project` explicitly to override. Use `azdo_context_set` to set session-level overrides in MCP mode. Session overrides are **process-wide** — correct for `stdio` (one client per process); under `http` they are shared across all connected clients, so prefer explicit `org`/`project` there.
- **`.azdo` file**: Place a `.azdo` JSON file (`{"org":"myorg","project":"myproject"}`; the simple `org=myorg` / `project=myproject` line format is also accepted) in any parent directory of a non-git folder to auto-detect org/project. Ignored when inside a git repository. A malformed `.azdo` file is skipped with a warning on stderr.
- **Concurrency**: In MCP mode the server handles up to 4 tool calls in parallel. Each HTTP request to Azure DevOps carries its own per-request `Basic` auth header, so concurrent calls to different organizations never cross credentials.
- **`voterId`**: Your Azure DevOps identity GUID (required for voting). Find it in your ADO profile URL: `https://dev.azure.com/{org}/_usersSettings/identities`.
- **Reviewer IDs in `azdo_pr_create`**: Must be Azure DevOps identity GUIDs, not email addresses. Use `azdo_identity_search` to resolve emails to GUIDs.
- **Code Search** (`azdo_code_search`): Requires the Azure DevOps search indexing to be enabled in the organization. Returns a clear error if disabled.
- **Wiki tools**: Require a project-level wiki. `wikiId` can be either the wiki UUID or the wiki name.
- **`azdo_wi_hierarchy`**: Returns the work item with its direct children. Use recursively to walk the full tree.
- **Identity & Group tools**: Require PAT with `vso.graph` scope (for Graph API) and `vso.memberentitlementmanagement` scope (for user entitlements). Without these scopes, 401/403 errors are returned.
- **`azdo_user_add` license types**: `express` (default, free Basic), `stakeholder` (free), `basic` (paid Basic), `advanced` (paid Basic+TestPlans), `basicAndTestPlans` (paid).

---

## ⚙️ Central config file

Single file for every org — `~/.config/devops-cli/config.json` (`$XDG_CONFIG_HOME/devops-cli/config.json` if set, `%APPDATA%\devops-cli\config.json` on Windows):

```json
{
  "defaultApiVersion": "7.1",
  "timeoutSeconds": 30,
  "cacheTtlSeconds": 60,
  "organizations": {
    "myorg": {
      "organizationUrl": "https://dev.azure.com/myorg",
      "pat": "<token>",
      "defaultProject": "MyProject",
      "description": null,
      "lastUpdated": "2026-01-01T00:00:00Z"
    }
  }
}
```

| Field | Meaning |
|---|---|
| `defaultApiVersion` | Azure DevOps REST API version used for same-host calls (default `7.1`). |
| `timeoutSeconds` | HTTP timeout for every request (default `30`). |
| `cacheTtlSeconds` | TTL for cached read responses in MCP mode (default `60`; `0`/negative → `60`). Writes evict the cache entries they invalidate, so a read right after a write sees fresh data. |
| `organizations` | Map of org name → `{ organizationUrl, pat, defaultProject, description, lastUpdated }`. |

The file is written with permissions `600` on Unix and saved atomically.
