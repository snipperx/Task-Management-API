# `.http` request files

Ready-to-run HTTP requests for every endpoint, for editors with a built-in HTTP client:

- **VS Code** — [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension
- **JetBrains Rider / IntelliJ** — built-in HTTP Client (the `.http` files and `http-client.env.json` are its native format)
- **Visual Studio 2022** — built-in `.http` support

## Usage

1. Start the API (`dotnet run --project src/TaskManagementAPI`, listening on `http://localhost:5252`).
2. Open any `.http` file.
3. Select the **`local`** environment (REST Client: status-bar picker; Rider: dropdown, top-right).
4. Click **Send Request** above a request. Run the `# @name login` request first in each
   file — later requests in that file reuse its token via `{{login.response.body.$.accessToken}}`.

Use the **`docker`** environment (`http://localhost:5000`) when running via `docker compose up`.

## Files

| File | Covers |
|---|---|
| `auth.http` | health, register, login (all seed roles), refresh, change-password, logout |
| `projects.http` | list/filter, create, get, update, project tasks, statistics, delete |
| `tasks.http` | list/filter/sort, overdue, statistics, CRUD, status/priority/assign, nested comments |
| `comments.http` | edit / delete a comment (author-only routes) |
| `users.http` | list, get, update, assign role, deactivate |

Credentials and base URLs live in `http-client.env.json`. Seed logins: `admin@company.com` /
`Admin@123`, `manager@company.com` / `Manager@123`, `dev1@company.com` / `Dev@123`,
`viewer@company.com` / `Viewer@123`.
