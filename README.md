# tgstation-server v4:

[![Build status](https://ci.appveyor.com/api/projects/status/7t1h7bvuha0p9j5f/branch/master?svg=true)](https://ci.appveyor.com/project/Cyberboss/tgstation-server-tools/branch/master) [![Build Status](https://travis-ci.org/tgstation/tgstation-server.svg?branch=master)](https://travis-ci.org/tgstation/tgstation-server) [![codecov](https://codecov.io/gh/tgstation/tgstation-server/branch/master/graph/badge.svg)](https://codecov.io/gh/tgstation/tgstation-server) [![Waffle.io - Columns and their card count](https://badge.waffle.io/tgstation/tgstation-server.png?columns=all)](https://waffle.io/tgstation/tgstation-server?utm_source=badge)

[![GitHub license](https://img.shields.io/github/license/tgstation/tgstation-server.svg)](https://github.com/tgstation/tgstation-server/blob/master/LICENSE) [![Average time to resolve an issue](http://isitmaintained.com/badge/resolution/tgstation/tgstation-server.svg)](http://isitmaintained.com/project/tgstation/tgstation-server "Average time to resolve an issue") [![NuGet version](https://img.shields.io/nuget/v/Tgstation.Server.Api.svg)](https://www.nuget.org/packages/Tgstation.Server.Api) [![NuGet version](https://img.shields.io/nuget/v/Tgstation.Server.Client.svg)](https://www.nuget.org/packages/Tgstation.Server.Client)

[![forthebadge](http://forthebadge.com/images/badges/made-with-c-sharp.svg)](http://forthebadge.com) [![forinfinityandbyond](https://user-images.githubusercontent.com/5211576/29499758-4efff304-85e6-11e7-8267-62919c3688a9.gif)](https://www.reddit.com/r/SS13/comments/5oplxp/what_is_the_main_problem_with_byond_as_an_engine/dclbu1a)

[![forthebadge](http://forthebadge.com/images/badges/built-with-love.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/60-percent-of-the-time-works-every-time.svg)](http://forthebadge.com)

This is a toolset to manage production BYOND servers. It includes the ability to update the server without having to stop or shutdown the server (the update will take effect on a "reboot" of the server) the ability start the server and restart it if it crashes, as well as systems for managing code and game files, and merging GitHub Pull Requests locally.  

### Legacy Servers

* Older server versions can be found in the V# branches of this repository

## Setup

### Pre-Requisites

- [.NET Core Runtime (>= v2.1)](https://www.microsoft.com/net/download) If you plan to install tgstation-server as a Windows service, you should also ensure that your .NET Framework runtime version is >= v4.7.1 (Download can be found on same page). On Windows, ensure that the `dotnet` executable file is in your system's `PATH` variable (or the user's that will be running the server).
- A [MariaDB](https://downloads.mariadb.org/), MySQL, or [Microsoft SQL Server](https://www.microsoft.com/en-us/download/details.aspx?id=55994) database engine is required

### Installation

1. [Download the latest V4 release .zip](https://github.com/tgstation/tgstation-server/releases/latest). The ServerService package will only work on Windows. Choose ServerConsole if that is not your target OS or you prefer not to use the Windows service.
2. Extract the .zip file to where you want the server to run from. Note the account running the server must have write access to the `lib` subdirectory.
3. If using the ServerService package, run `Tgstation.Server.Host.Service.exe`. It should prompt you to install the service. Click `Yes` and accept a potential UAC elevation prompt. You should now be able to control the service using the Windows service control commandlet.

#### Linux

[We recommend using Docker for Linux installations](https://github.com/tgstation/tgstation-server#docker). The content of this parent section may be skipped if you choose to do so

The following dependencies are required to run tgstation-server on Linux alongside the .NET Core runtime

- gcc-multilib (on 64-bit systems for running BYOND)

Note that tgstation-server has only ever been tested on Linux via it's [docker environment](https://github.com/tgstation/tgstation-server/blob/master/build/Dockerfile#L22). If you are having trouble with something, or figure out a required workaround, please contact project maintainers so this documentation may be better updated.

#### Docker

tgstation-server supports running in a docker container and is the recommended deployment method for Linux systems due being the only robustly tested environment. The official image repository is located at https://hub.docker.com/r/tgstation/server. It can also be built locally by running `docker build . -f build/Dockerfile` in the repository root.

To create a container run
```sh
docker run \
	-ti \ #start interactive for manual configuration
	--restart=always \ #if you want maximum uptime
	--network="host" \ #if your sql server is on the same machine
	--name="tgs" \ #or whatever else you wanna call it
	--cap-add=sys_nice \ #allows tgs to schedule DreamDaemon as a higher priority process
	--init \ #reaps potential zombie processes
	-p <tgs port>:80 \
	-p 0.0.0.0:<public game port>:<internal game port> \
	-v /path/to/store/instances:/tgs4_instances \
	-v /path/to/your/appsettings.Production.json:/config_data \ #only if you want to use manual configuration
	-v path/to/your/log/folder:/tgs_logs \
	tgstation/server
```
with any additional options you desire (i.e. You'll have to expose more game ports in order to host more than one instance).

Note although `/app/lib` is specified as a volume mount point in the `Dockerfile`, unless you REALLY know what you're doing. Do not mount any volumes over this for fear of breaking your container.

If using manual configuration, before starting your container make sure the aforemention `appsettings.Production.json` is setup properly. See below

### Configuring

The first time you run TGS4 you should be prompted with a configuration wizard which will guide you through setting up your appsettings.Production.json 

![](https://user-images.githubusercontent.com/8171642/46436355-99ee0e00-c726-11e8-82fa-6626b2503a6c.png)

This wizard will, generally, run whenever the server is launched without detecting the config json. Follow the instructions below to perform this process manually.

#### Manual Configuration

Create an `appsettings.Production.json` file next to `appsettings.json`. This will override the default settings in appsettings.json with your production settings. There are a few keys meant to be changed by hosts. Modifying any config files while the server is running will trigger a safe restart (Keeps DreamDaemon's running). Note these are all case-sensitive: 

- `General:MinimumPasswordLength`: Minimum password length requirement for database users

- `General:GitHubAccessToken`: Specify a GitHub personal access token with no scopes here to highly mitigate the possiblity of 429 response codes from GitHub requests

- `FileLogging:Directory`: Override the default directory where server logs are stored. Default is C:/ProgramData/tgstation-server/logs on Windows, /usr/share/tgstation-server/logs otherwise

- `FileLogging:LogLevel`: Can be one of `Trace`, `Debug`, `Information`, `Warning`, `Error`, or `Critical`. Restricts what is put into the log files. Currently `Debug` is reccommended for help with error reporting.

- `Kestrel:Endpoints:Http:Url`: The URL (i.e. interface and ports) your application should listen on. General use case should be `http://localhost:<port>` for restricted local connections. See the Remote Access section for configuring public access to the World Wide Web. This doesn't need to be changed using the docker setup and should be mapped with the `-p` option instead

- `Database:DatabaseType`: Can be one of `SqlServer`, `MariaDB`, or `MySql`

- `Database:MySqlServerVersion`: The version of MySql/MariaDB the database resides on, can be left as null for attempted auto detection. Used by the MySQL/MariaDB provider for selection of [certain features](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/2.1.1/src/EFCore.MySql/Storage/Internal/ServerVersion.cs) ignore at your own risk. A string in the form `<major>.<minor>.<patch>`

- `Database:ConnectionString`: Connection string for your database. Click [here](https://www.developerfusion.com/tools/sql-connection-string/) for an SQL Server generator or see [here](https://www.connectionstrings.com/mysql/) for a MySQL guide.

### Database Configuration

If using MySQL, our provider library [recommends you set 'utf8mb4' as your default charset](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql#1-recommended-server-charset) disregard at your own risk.

The user created for the application will need the privilege to create databases on the first run, do not create the database for it. Once the initial set of migrations is run, the create right may be revoked. The user should maintain DDL rights though for applying future migrations

Note that the ratio of application installations to databases is 1:1. Do not attempt to share a database amongst multiple TGS installations. (We know SQLite would be perfect for this, but it does not handle the high level of concurrency the server uses)

### Starting

For the Windows service version start the `tgstation-server-4` service. If it fails to start, check the Windows event log under Windows/Application for entries from tgstation-server-4 for errors.

For the console version run `dotnet Tgstation.Server.Host.Console.dll` in the installation directory. The `tgs.bat` and `tgs.sh` shell scripts are shortcuts for this. If on Windows, you must do this as admin to give the server permission to install the required DirectX dependency for BYOND as well as create symlinks.

For the docker version run `docker start tgs`

### Stopping

Note that the live detach for DreamDaemon servers is only supported for updates or restarts via the API at this time. Stopping tgstation-server will TERMINATE ALL CHILD DREAMDAEMON SERVERS.

For the Windows service version stop the `tgstation-server-4` service

For the console version press `Ctrl+C` or send a SIGQUIT to the ORIGINAL dotnet process

For the docker version run `docker stop tgs`

## Integrating

A breaking change from V3: tgstation-server 4 now REQUIRES the DMAPI to be integrated into any BYOND codebase which plans on being used by it. The integration process is a fairly simple set of code changes.

1. Copy the [DMAPI](https://github.com/tgstation/tgstation-server/tree/master/src/DMAPI) files anywhere in your code base. `tgs.dm` can be seperated from the `tgs` folder, but do not modify or move the contents of the `tgs` folder
2. Modify your `.dme`(s) to include the `tgs.dm` and `tgs/includes.dm` files (ORDER OF APPEARANCE IS MANDATORY)
3. Follow the instructions in `tgs.dm` to integrate the API with your codebase.

The DMAPI is fully backwards compatible and should function with any tgstation-server version to date. Updates can be performed in the same manner. Using the `TGS_EXTERNAL_CONFIGURATION` is recommended in order to make the process as easy as replacing `tgs.dm` and the `tgs` folder with a new version

### Example

Here is a bare minimum example project that implements the essential code changes for integrating the DMAPI

Before `tgs.dm`:
```
//Remember, every codebase is different, you probably have better methods for these defines than the ones given here
#define TGS_EXTERNAL_CONFIGURATION
#define TGS_DEFINE_AND_SET_GLOBAL(Name, Value) var/global/##Name = ##Value
#define TGS_READ_GLOBAL(Name) global.##Name
#define TGS_WRITE_GLOBAL(Name, Value) global.##Name = ##Value
#define TGS_WORLD_ANNOUNCE(message) world << ##message
#define TGS_INFO_LOG(message) world.log << "TGS Info: [##message]"
#define TGS_ERROR_LOG(message) world.log << "TGS Error: [##message]"
#define TGS_NOTIFY_ADMINS(event) world.log << "TGS Admin Message: [##event]"
#define TGS_CLIENT_COUNT global.client_cout
#define TGS_PROTECT_DATUM(Path) // Leave blank if your codebase doesn't give administrators code reflection capabilities
```

Anywhere else:
```dm
var/global/client_count = 0

/world/New()
	..()
	TgsNew()
	TgsInitializationsComplete()

/world/Reboot()
	TgsReboot()
	..()

/world/Topic()
	TGS_TOPIC
	..()

/client/New()
	..()
	++global.client_count

/client/Del()
	..()
	--global.client_count

```

## Remote Access

tgstation-server is an [ASP.Net Core](https://docs.microsoft.com/en-us/aspnet/core/) app based on the Kestrel web server. This section is meant to serve as a general use case overview, but the entire Kestrel configuration can be modified to your liking with the configuration JSON. See [the official documentation](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel) for details.

Exposing the builtin Kestrel server to the internet directly over HTTP is highly not reccommended due to the lack of security. The recommended way to expose tgstation-server to the internet is to host it through a reverse proxy with HTTPS support. Here are some step by step examples to achieve this for major web servers.

System administrators will most likely have their own configuration plans, but here are some basic guides for beginners.

Once complete, test that your configuration worked by visiting your proxy site from a browser on a different computer. You should recieve a 401 Unauthorized response.

### IIS (Reccommended for Windows)

1. Acquire an HTTPS certificate. The easiet free way for Windows is [win-acme](https://github.com/PKISharp/win-acme) (requires you to set up the website first)
2. Install the [Web Platform Installer](https://www.microsoft.com/web/downloads/platform.aspx)
3. Open the web platform installer in the IIS Manager and install the Application Request Routing 3.0 module
4. Create a new website, bind it to HTTPS only with your chosen certificate and exposed port. The physical path won't matter since it won't be used. Use `Require Server Name Indication` if you want to limit requests to a specific URL prefix.
5. Close and reopen the IIS Manager
5. Open the site and navigate to the `URL Rewrite` module
6. In the `Actions` Pane on the right click `Add Rule(s)...`
7. For the rule template, select `Reverse Proxy` under `Inbound and Outbound Rules` and click `OK`
8. You may get a prompt about enabling proxy functionality. Click `OK`
9. In the window that appears set the `Inbound Rules` textbox to the URL of your tgstation-server i.e. `http://localhost:5000`. Ensure `Enable SSL Offloading` is checked, then click `OK`

### Caddy (Reccommended for Linux, or those unfamilar with configuring NGINX or Apache)

1. Setup a basic website configuration. Instructions on how to do so are out of scope.
2. In your Caddyfile, under a server entry, add the following (replace 8080 with the port TGS is hosted on):
```
proxy /tgs localhost:8080 {
	transparent
}
```

See https://caddyserver.com/docs/proxy

### NGINX (Reccommended for Linux)

1. Setup a basic website configuration. Instructions on how to do so are out of scope.
2. Acquire an HTTPS certificate, likely via Let's Encrypt, and configure NGINX to use it.
3. Setup a path under a server like the following (replace 8080 with the port TGS is hosted on):
```
location /tgs {
	proxy_pass http://127.0.0.1:8080;
	break;
}
```

See https://docs.nginx.com/nginx/admin-guide/web-server/reverse-proxy/

### Apache

1. Ensure the `mod_proxy` extension is installed. 
2. Setup a basic website configuration. Instructions on how to do so are out of scope.
3. Acquire an HTTPS certificate, likely via Let's Encrypt, and configure Apache to use it.
4. Under a VirtualHost entry, setup the following (replace 8080 with the port TGS is hosted on):
```
ProxyPass / http://127.0.0.1:8080
ProxyPassReverse / http://127.0.0.1:8080
```

See https://httpd.apache.org/docs/2.4/howto/reverse_proxy.html

## Usage

tgstation-server v4 is controlled via a RESTful HTTP json API. Documentation on this API can be found [here](https://tgstation.github.io/tgstation-server/api.html). This section serves to document the concepts of the server.

### Users

All actions apart from logging in must be taken by a user. TGS installs with one default user whose credentials can be found [here](https://github.com/tgstation/tgstation-server/blob/master/src/Tgstation.Server.Api/Models/User.cs). It is recommended to disable this user ASAP as it is used to create Jobs that are started by the system. If access to all users is lost, the default user can be reset using the `Database:ResetAdminPassword` configuration setting. 

Users can be enabled/disabled and have a very granular set of rights associated to them that determine the actions they are allowed to take (i.e. Modify the user list or create instances). Users can be _database based_ or _system based_. Database users are your standard web users with a username and password. System users, on the otherhand, are authenticated with the host OS. These users cannot have their password or names changed by TGS as they are managed by the system (and in reverse, login tokens don't expire when their password changes). The benefit to having these users is it allows the use of system ACLs for static file control. More on that later.

### Instances

A TGS deployment is made up with a set of instances, which each represent a production BYOND server. As many instances as desired can be created. Be aware, however, due to the nature of BYOND, this will quickly result in system resource exhaustion.

An instance is stored in a single folder anywhere on a system and is made up of several components: The source code git repository, the BYOND installations, the compiler, the watchdog, chat bots, and static file management systems.

##### Instance Users

All users with access to an instance have an InstanceUser object associated with the two that defines more rights specific to that instance (i.e. Deploy code, modify bots, edit other InstanceUsers). 

#### Repository

The `Repository` folder is a git repository containing the code of the game you wish to host. It can be cloned from any public or private remote repository and has capabilities to affect changes back to it. All the standard benefits of git are utilized (i.e. check out any revision or reference).

Additional features become available if the remote repository is hosted on https://github.com/. Namely the Test Merge feature, which allows you to take a pull request opened on the repository and compile it into a game deployment for testing. Information about test merges is available in game via the DMAPI and via the main API as well.

Manual operations on the repository while an instance is running may lead to git data corruption. Thankfully, it's simple enough to delete and reclone the repository via the API.

#### Byond

The `Byond` folder contains installations of [BYOND](https://secure.byond.com/) versions. The version which is used by your game code can be changed on a whim (Note that only versions >= 511.1385 have been thouroughly tested. Lower versions should work but if one doesn't function, please open an issue report) and the server will take care of installing it.

#### Compiler

The compiler deploys code from the `Repository` folder to the `Game` folder and compiles it either by autodetecting the `.dme` or having it set by configuration. Several other step are also run such as validating the DMAPI version and creating symlinks for static files are done at this point. The compiler also applies server side code modifications and duplicates compiled code for the watchdog as well (See following section).

#### Watchdog

The watchdog is responsible for starting and keeping your server running. It functions by launching two servers which are hot-swapped on `/world/Reboot`s and during crashes to prevent downtime. This hot swapping feature is also what allows TGS to deploy updates to live servers without bringing them down.

DreamDaemon can be finicky and will crash with several high load games or bad DM code. The watchdog has several failure prevention methods to keep at least one server running while these issues are sorted out.

#### Chat Bots

TGS supports creating infinite chat bots for notifying staff or players of things like code deployments and uptime in. Currently the following providers are supported

- Internet Relay Chat (IRC)
- Discord

More can be added by providing a new implementation of the [IProvider](https://github.com/tgstation/tgstation-server/blob/master/src/Tgstation.Server.Host/Components/Chat/Providers/IProvider.cs) interface

Bots have a set of built-in commands that can be triggered via `!tgs`, mentioning, or private messaging them. Along with these, custom commands can be defined using the DMAPI by creating a subtype of the `/datum/tgs_chat_command` type (See `tgs.dm` for details). Invocation for custom commands can be restricted to certain channels.

#### Static Files

All files in game code deployments are considered transient by default, meaning when new code is deployed, changes will be lost. Static files allow you to specify which files and folders stick around throughout all deployments.

The `StaticFiles` folder contains 3 root folders which cannot be deleted and operate under special rules
	- `CodeModifications`
	- `EventScripts`
	- `GameStaticFiles`

These files can be modified either in host mode or system user mode. In host mode, TGS itself is responsible for reading and writing the files. In system user mode read and write actions are performed using the system account of the logged on User, enabling the use of ACLs to control access to files. Database users will not be able to use the static file system if this mode is configured for an instance.

This folder may be freely modified manually just beware this may cause in-progress deployments to error if done on Windows systems.

#### CodeModifications

When a deployment is made by the compiler, all the contents of this folder are copied over the repository contents. Then one of two code change modes are selected based on the prescense of certain files.

If `<target dme>.dm` is present, that .dme will be used instead of the repository's `.dme`

Otherwise the files `HeadInclude.dm` and `TailInclude.dm` are searched for and added as include lines to the top and bottom of the target `.dme` repsectively if they exist. These files can contain any valid DreamMaker code (Including `#include`ing other `.dm` files!) allowing you to modify the a repository's code on a per instance basis

#### EventScripts

This folder can contain anything. But, when certain events occur in the instance, TGS will look here for `.bat` or `.sh` files with the same name and run those with corresponding arguments. List of supported events can be found [here](https://github.com/tgstation/tgstation-server/blob/master/src/Tgstation.Server.Host/Components/StaticFiles/Configuration.cs#L28) (subject to expansion) list of event parameters can be found [here](https://github.com/tgstation/tgstation-server/blob/master/src/Tgstation.Server.Host/Components/EventType.cs)

#### GameStaticFiles

Any files and folders contained in this root level of this folder will be symbolically linked to all deployments at the time they are created. This allows persistent game data (BYOND `.sav`s or code configuration files for example) to persist across all deployments.

### Updating

TGS 4 can self update without stopping your DreamDaemon servers. Any V4 release made to this repository is bound by a contract that allows changes of the runtime assemblies without stopping your servers. Database migrations are automatically applied as well.

### Clients

Here are some tools for interacting with the TGS 4 JSON API

- [Tgstation.Server.ControlPanel](https://github.com/tgstation/Tgstation.Server.ControlPanel): Official client. A cross platform GUI for using tgstation-server
- [Tgstation.Server.Client](https://www.nuget.org/packages/Tgstation.Server.Client): A nuget .NET Standard 2.0 TAP based library for communicating with tgstation-server
- [Tgstation.Server.Api](https://www.nuget.org/packages/Tgstation.Server.Api): A nuget .NET Standard 2.0 library containing API definitions for tgstation-server
- [Postman](https://www.getpostman.com/): This repository contains [TGS.postman_collection.json](https://github.com/tgstation/tgstation-server/blob/master/tools/TGS.postman_collection.json) which is used during development for testing. Contains example requests for all endpoints but takes some knowledge to use (Note that the pre-request script is configured to login the default admin user for every request)

Contact project maintainers to get your client added to this list

## Troubleshooting

Feel free to ask for help at the coderbus discord: https://discord.gg/Vh8TJp9. Cyberboss#8246 can answer most questions.

## Contributing

* See [CONTRIBUTING.md](https://github.com/tgstation/tgstation-server/blob/master/.github/CONTRIBUTING.md)

## Licensing

* The DMAPI for the project is licensed under the MIT license.
* The /tg/station 13 icon is licensed under [Creative Commons 3.0 BY-SA](http://creativecommons.org/licenses/by-sa/3.0/).
* The remainder of the project is licensed under [GNU AGPL v3](http://www.gnu.org/licenses/agpl-3.0.html)

See the files in the /src/DMAPI tree for the MIT license
