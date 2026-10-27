# Closed Services Admin

Closed Services Admin is a [.NET Blazor](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor) based system for users managing service closures in local authorities, designed and developed by Dorset Council.

Closed Services Admin is used by Dorset Council to allow staff and service managers to update their service closures, and to allow the public to view the current status of services.

Closed Services Admin is Open Source software and is licenced under the MIT licence.

## Features

Closed Services Admin is a .NET Blazor web application for managing service closures in a local authority:

- Service closure management
	- Add and delete service closures
	- Set up closures for Today, Tomorrow, or a specific date or time range
- Customisable service types
	- Set up your own list of service types, such as Schools, Libraries, Leisure Centres, etc.
- Customisable service closure reasons
	- Set up your own list of closure reasons, such as "Weather", "Staff Shortage", "Planned Maintenance", etc.
- User management
	- Users can be assigned to individual services, or entire service types
	- Uses Microsoft Entra External ID or other compatible OpenID connect provider for user authentication
	- Users can be assigned as Admin to manage other users access to services
- Mobile friendly
- Nothing for your users to install, they just need a web browser
- Uses modern tech, including .NET, Blazor, PostGIS and Entra External ID

## Bugs
Please use the issue tracker for all bugs and feature requests. Before creating a new issue, do a quick search to see if the problem has been reported already.

Dorset Council staff should submit issues via the internal help desk.

## Dependencies

To run Closed Services Admin with minimal modification, you will need:
- A web server capable of running .NET applications, such as IIS, Kestrel or Azure
- PostgreSQL 13+ with PostGIS extension
- An Entra External ID tenant

Closed Services Admin uses [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/). Postgres has been set up for this project, but with some modifications, any Entity Framework Core compatible provider should work. For a full list of providers, check the [Entity Framework docs](https://docs.microsoft.com/en-us/ef/core/providers/?tabs=dotnet-core-cli).

## Contributing

Please see our guide on [contributing](CONTRIBUTING.md) if you're interested in getting involved.

## Reporting security issues

Security issues should be reported privately, via email, to the Dorset Council GIS team gis@dorsetcouncil.gov.uk. You should receive a response within 24 hours during business days.

## Core developers

Closed Services Admin is a Dorset Council Open Source project. The Dorset Council software development team are the core developers of the project. If you would like to get involved, please see our [contributing guide](CONTRIBUTING.md).

## Alternatives

Closed Services Admin is a Dorset Council project and has been built according to our particular needs. Whilst we believe the project can be easily used and adapted
by others, and is fairly flexible, there are other alternatives out there that may fit your needs better. We encourage you to do your own research, as a commercial solution may be more appropriate for your needs.

## Acknowledgements

Closed Services Admin would not be possible without the open source community. This is just a small list of our favourite open source projects and organisations that have helped us:

- [.NET](https://dot.net)
- [Postgres](https://www.postgresql.org/)/[PostGIS](https://postgis.net/)
- [TypeScript](https://typescriptlang.org)
- [Government Digital Service design system](https://design-system.service.gov.uk/)
- [GDS Blazor Components](https://github.com/Dorset-Council-UK/GdsBlazorComponents)

## Licencing
Unless stated otherwise, the codebase is released under the MIT License. This covers both the codebase and any sample code in the documentation.

The documentation is © Dorset Council and available under the terms of the Open Government 3.0 licence.

