## Using the Application

- By default, the main developer UI is exposed at; https://localhost:8080/
- The Swagger API and documentation is available at; https://localhost:8080/api/
- Which is also exposed directly at; http://localhost:5000/api/

# Running the Application on Docker

## Management Script

The `manage` script wraps the Docker process in easy to use commands.

To get full usage information on the script, run:

```
./manage -h
```

### Build all containers
```bash
./manage build
```

### Build specific container
```bash
./manage build api
```

### Start services
```bash
./manage start
```

### Start in debug mode (with hot reload)
```bash
./manage debug
```

### Stop services
```bash
./manage stop
```

### Remove containers and volumes
```bash
./manage down
# or
./manage rm
```

# Dev Container

The VS Code devcontainer starts through `.devcontainer/docker-compose.yaml`. Docker Compose creates the
`unified-scheduling-dev` network and a local `db` service automatically, so a fresh developer does not need to
create the Docker network manually before opening the devcontainer. Inside the devcontainer, the database is
reachable at host `db` on port `5432`.
