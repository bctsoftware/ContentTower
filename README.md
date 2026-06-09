# ContentTower
<img src="content-tower-icon.svg"/>

## The idea
ContentTower is an open-source stand-alone webservice that holds data for you. Fairly simple, really. It takes care to follow a loose storage quota, as well as clean up old files.

## QuickStart
I'm in a hurry. How do I use ContentTower?
- Have Docker installed
- Download this [docker-compose](./docker-compose.yml) file.
- Run `docker-compose up -d`
- Visit http://localhost:8082 to check out ContentTower.
- Generate an OpenAPI client for your project using this [openapi.json](./ContentTower.IntegrationTests/openapi.json) file.

## Store Types
ContentTower allows you to store content in one of three modes:
- `Default` - Default option. Content is stored for a fixed length of time after upload.
- `Temporary` - Content is stored for a short duration after last activity. Upload and download activity resets this timer.
- `Permanent` - ContentTower will not delete this content, nor will it allow the normal delete operation to remove this content.

## Configuration
The following aspects of ContentTower can be easily configured. You can use the standard dotnet appsettings approach, or environment variables.
| Name | Description | Example |
| -----|-------------|---------|
| Storage__Quota | Storage limit. Once reached, new uploads will not be accepted. | "1073741824" |
| Storage__DataPath | Path where ContentTower will store its data. Total disk usage can exceed provided quota. | "/app/data" |
| Storage__CleanupIntervalSeconds | Controls frequency with which old content is detected and removed. | "600" |
| Storage__StoreDurationDefaultNominalSeconds | Content stored with 'Default' storeType is retained for this many seconds under normal conditions. | "2592000" |
| Storage__StoreDurationDefaultPressureSeconds | Content stored with 'Default' storeType is retained for this many seconds when quota is under pressure. | "1296000" |
| Storage__StoreDurationTemporaryNominalSeconds | Content stored with 'Temporary' storeType is retained for this many seconds under normal conditions. | "86400" |
| Storage__StoreDurationTemporaryPressureSeconds | Content stored with 'Temporary' storeType is retained for this many seconds when quota is under pressure.  | "10800" |

## Building and Testing
Once you clone this repository, it should be as simple as `dotnet build` and `dotnet test`.

## Integration tests
A few integration tests are built inside a docker container. You can use them to see how ContentTower is intended to be used.
Once you clone this repository, go into `ContentTower.IntegrationTests` and run `docker-compose build` and `docker-compose up -d`.
These tests might take a while.

## Found a problem?
Please create an issue.

## Want a feature?
Please create an issue.

## Built something cool for ContentTower?
Please create a PR.

## Build something cool using ContentTower?
Please reach out, I've love to see.

## Enjoying ContentTower?
Please Star and/or Watch this repository.

## Using ContentTower in your highly profitable and successful commercial venture?
Please consider making a donation to support this small project. Reach out via my website contact information.

