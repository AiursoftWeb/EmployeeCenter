# EmployeeCenter

[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/aiursoftweb/employeeCenter/blob/master/LICENSE)
[![Pipeline stat](https://gitlab.aiursoft.com/aiursoft/employeeCenter/badges/master/pipeline.svg)](https://gitlab.aiursoft.com/aiursoft/employeeCenter/-/pipelines)
[![Test Coverage](https://gitlab.aiursoft.com/aiursoft/employeeCenter/badges/master/coverage.svg)](https://gitlab.aiursoft.com/aiursoft/employeeCenter/-/pipelines)
[![Man hours](https://manhours.aiursoft.com/r/github.com/aiursoftweb/employeeCenter.svg)](https://manhours.aiursoft.com/r/github.com/aiursoftweb/employeeCenter.html)
[![Website](https://img.shields.io/website?url=https%3A%2F%2Femployee.aiursoft.com)](https://employee.aiursoft.com)
[![Docker](https://img.shields.io/docker/pulls/aiursoft/employeecenter.svg)](https://hub.docker.com/r/aiursoft/employeecenter)

Employee Center is a project for managing employee information, attendance, leaves, and more. It is built with ASP.NET Core and provides a user-friendly interface for both employees and administrators.

![screenshot](./screenshot.png)

Default user name is `admin@default.com` and default password is `Admin@123456!`.

## Try

Try a running Employee Center [here](https://employee.aiursoft.com).

## Run in Ubuntu

The following script will install\update this app on your Ubuntu server. Supports Ubuntu 25.04.

On your Ubuntu server, run the following command:

```bash
curl -sL https://github.com/aiursoftweb/employeeCenter/raw/master/install.sh | sudo bash
```

Of course it is suggested that append a custom port number to the command:

```bash
curl -sL https://github.com/aiursoftweb/employeeCenter/raw/master/install.sh | sudo bash -s 8080
```

It will install the app as a systemd service, and start it automatically. Binary files will be located at `/opt/apps`. Service files will be located at `/etc/systemd/system`.

## Run manually

Requirements about how to run

1. Install [.NET 10 SDK](http://dot.net/) and [Node.js](https://nodejs.org/).
2. Execute `npm install` at `wwwroot` folder to install the dependencies.
3. Execute `dotnet run` to run the app.
4. Use your browser to view [http://localhost:5000](http://localhost:5000).

## Business qualifications and watermarked copies

- **Career → Resources → Business Qualifications**: authenticated employees specify a concrete business purpose and recipient company, then generate, preview and download a personalized PNG copy. No approval is required in this version. The applicant name is read server-side from `LegalName`, falling back to `DisplayName`.
- **Administration → Legal → Manage Business Qualifications**: assign the `CanManageQualifications` permission to administrators who add originals, enable or disable versions, inspect all records and detect embedded tracking IDs. Existing deployments must grant this new permission through role management and sign in again; only a newly seeded administrator role automatically receives all permissions. Each addition is an immutable version associated with a company entity and is enabled by default. `/Qualifications` only offers **Generate Watermark and Download** and **My Download Records**, even for administrators; `/ManageQualifications` offers **Add Qualification**, **Stop Use / Enable**, **All Download Records** and watermark detection. Disabling blocks new generation and existing image previews/downloads while retaining originals and records; re-enabling restores both. Already downloaded files cannot be recalled.
- Originals and generated copies are stored under the private vault's `qualifications/` directory. Generic download routes cannot serve this directory, even with file tokens. Copies are accessible only to their applicant and qualification administrators. Generation creates a durable issuance record before returning a preview; explicit downloads record a count and the last request time (not proof that transfer completed). Employee record lists always contain only the signed-in applicant's records; all-user records and read-only trace details live exclusively in the management area.
- Inputs: PNG/JPEG, up to 20 MiB, both dimensions at least 512 pixels and at most 25 million decoded pixels. File pickers use `image/png,image/jpeg` MIME types for compatibility with UIStack's client-side file validator; the server independently validates extensions and image contents. PDF originals are not accepted. Output is a flattened PNG with no source EXIF; images whose longest side exceeds 2048 pixels are proportionally reduced, with both output dimensions still at least 512 pixels. Visible watermarks contain the purpose, recipient, account-derived name, UTC time and unique tracking ID. Invisible watermarks contain only the ID, which maps to the retained qualification/version, applicant, purpose, recipient, timestamps and SHA-256 hashes in the database.
- Install a CJK font on non-container hosts (Ubuntu: `sudo apt install fonts-noto-cjk`). Docker installs it automatically. Set `Qualifications:WatermarkFontPath` (or environment variable `Qualifications__WatermarkFontPath`) to use a local CJK font file. A missing suitable font fails generation rather than returning an unwatermarked original.
- The administrator detection form processes uploads locally without retaining them. A match identifies a generated copy, **not proof that its applicant misused it**. No match does not prove absence of a watermark. Resizing, cropping, screenshots, geometric transformations and printing/scanning may destroy the signal. This is a tracing aid, not DRM or a tamper-proof signature.
- Back up the database, private vault and application data-protection keys. Generated records/copies are retained for tracing; establish an appropriate retention policy before production use.

**Migration warning:** existing Company Entity license/certificate attachments and their old download URLs are not automatically moved or revoked. Before rollout, add the original into this feature, remove old attachment links and delete/restrict old publicly accessible files and copies. Do not commit actual certificates to the repository. Both SQLite and MySQL schema migrations are generated against the latest model (not copied from the old project) and applied through the existing startup migration mechanism.

Run the focused validation suite with:

```bash
dotnet test tests/Aiursoft.EmployeeCenter.Tests.csproj \
  --filter 'FullyQualifiedName~QualificationsTests|FullyQualifiedName~QualificationWatermarkServiceTests|FullyQualifiedName~QualificationPersistenceTests|FullyQualifiedName~MigrationTests'
```

## Run in Microsoft Visual Studio

1. Open the `.sln` file in the project path.
2. Press `F5` to run the app.

## Run in Docker

First, install Docker [here](https://docs.docker.com/get-docker/).

Then run the following commands in a Linux shell:

```bash
image=aiursoft/employeecenter
appName=employeecenter
sudo docker pull $image
sudo docker run -d --name $appName --restart unless-stopped -p 5000:5000 -v /var/www/$appName:/data $image
```

That will start a web server at `http://localhost:5000` and you can test the app.

The docker image has the following context:

| Properties  | Value                           |
|-------------|---------------------------------|
| Image       | aiursoft/employeecenter         |
| Ports       | 5000                            |
| Binary path | /app                            |
| Data path   | /data                           |
| Config path | /data/appsettings.json          |

## How to contribute

There are many ways to contribute to the project: logging bugs, submitting pull requests, reporting issues, and creating suggestions.

Even if you with push rights on the repository, you should create a personal fork and create feature branches there when you need them. This keeps the main repository clean and your workflow cruft out of sight.

We're also interested in your feedback on the future of this project. You can submit a suggestion or feature request through the issue tracker. To make this process more effective, we're asking that these include more information to help define them more clearly.
