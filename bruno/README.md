# Bruno collection

Git-friendly API collection (plain-text `.bru` files) for [Bruno](https://www.usebruno.com/) —
an open-source, offline Postman alternative. No cloud account, no sync.

## Open it

- **GUI**: Bruno → *Open Collection* → pick this `bruno/` folder. Choose the **Local** or
  **Docker** environment (top-right), then run **Auth / Login** before anything else.
- **CLI**: `npm i -g @usebruno/cli`, then from `bruno/`:

  ```bash
  bru run --env Local                       # whole collection
  bru run Auth Tasks --env Local            # selected folders, in order
  bru run --env Docker --reporter-junit out.xml
  ```

## How it hangs together

- **`collection.bru`** sets collection-wide bearer auth to `{{accessToken}}` and a JSON
  `Content-Type`. Every request uses `auth: inherit` except the auth endpoints (`auth: none`).
- **Auth / Login** (and the `Login as ...` variants) run a post-response script that stores
  `accessToken`, `refreshToken`, and `currentUserId` as environment variables.
- Chained requests pass ids along the same way: `Create Project` → `projectId`,
  `Create Task` → `taskId`, `Add Comment` → `commentId`, `List Users` → `userId`.
- Run folders in order (Auth → Projects → Tasks → Comments → Users) for a clean end-to-end pass.

## Folders

| Folder | Requests |
|---|---|
| Auth | Health, Register, Login (+ per-role variants), Refresh, Change Password, Logout |
| Projects | List, Create, Get, Update, Project Tasks, Statistics, Delete |
| Tasks | List/filter, Overdue, Statistics, Create, Get, Update, Change Status/Priority, Assign, Comments, Delete |
| Comments | Edit, Delete (author-only routes) |
| Users | List, Get, Update, Assign Role, Deactivate |

Query params prefixed `~` in the `.bru` files are disabled by default — toggle them on in the
GUI (Params tab) to exercise filters.
