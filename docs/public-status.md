# Public service status

## Hosting and rollout

Deploy `ClosedServices-Admin.Api` first as a separate anonymous HTTPS application. Aspire AppHost registers `closedservices-admin-api` against the existing `closedservices-admin` database and waits for the migration service. Start AppHost using its HTTPS launch profile. The API has no dependency on the admin web executable, identity provider, or admin cookies.

When hosting independently, supply `ConnectionStrings__closedservices-admin` securely, and set `ClosedServices__Database__Schema` to the same schema as the migration service (`closedservices` by default). Use a read-only database credential for the API and, where practical, for public queries. Migrations remain the responsibility of the migration service, not the API.

Configure `ClosedServices__PathBase` when hosting under a subpath (empty by default for API), `AllowedHosts` for production hostnames, and `ClosedServices__UseHttpsRedirection` when HTTP-to-HTTPS redirection is needed. For a trusted reverse proxy, configure `ClosedServices__Networking__UseForwardedHeadersMiddleware` and `ClosedServices__Networking__KnownProxies`. Never trust arbitrary forwarded headers. Terminate HTTPS at the ingress or serve HTTPS directly. No deployment manifests are currently present in this repository: external ingress and proxy configuration must be verified by the deployer.

Telemetry uses the existing ServiceDefaults/OpenTelemetry configuration, including `OTEL_EXPORTER_OTLP_ENDPOINT` and rate-limiter metrics. Each application configures `PublicStatus:RateLimit:PermitLimit` (default 120) and `PublicStatus:RateLimit:WindowSeconds` (default 60). Limits apply per client IP and per application instance, only to public status endpoints. Tune them for many visitors sharing a NAT/proxy and legitimate one-minute refresh traffic. A trusted proxy must forward client IPs. Add edge/distributed protection if traffic or replica count requires it.

Before enabling either public surface, review **all closure messages and reason names for public publication**. API messages are always public. `ShowMessages=false` is a widget presentation setting, not an access-control boundary. Inactive services and internal updater identities, permissions, geometry, source identifiers, and expired history are not published.

## API

Read-only, unauthenticated endpoints:

- `GET /api/v1/services/{serviceId}/status` returns `{ generatedAt, service }`. Optional `ServiceType` restricts the lookup to that type.
- `GET /api/v1/services/status?ServiceType=Libraries` returns `{ generatedAt, services }` for all active services of that type.
- `GET /api/v1/services/status?ServiceID={guid}` returns the collection form for one service.
- Both filters can be combined and must match; there is no unfiltered all-services endpoint.

Query parameter names and service-type lookup are case-insensitive; type names are trimmed. URL-encode names containing spaces or special characters. IDs are nonempty GUIDs. Conflicting duplicate filter values, invalid IDs, and missing filters return **400**. Unknown types, unknown/inactive services, and type/ID mismatches return **404**. A known type without active services returns **200** with an empty array. Errors use JSON Problem Details, including **429** with `Retry-After` and generic **500** errors; no sign-in redirect or HTML admin page is returned.

Each service exposes `serviceId`, `name`, `serviceType`, `status`, `currentClosure`, and `upcomingClosures`. Status values are `noDisruption`, `closed`, and `partiallyClosed`. Closure fields are `status`, `effectiveFrom`, `effectiveTo`, `reason`, and `message`. Timestamps are ISO 8601 UTC; a null end represents an indefinite closure. `generatedAt` records when status was evaluated, not the last administrative edit. No current override means **no disruption reported**, not that a service is open during its normal operating hours.

Status responses and widget documents are `Cache-Control: no-store` so closure boundaries are recalculated on every request. Browser cross-origin API fetching is not enabled by default; iframe embedding does not require CORS.

## Widget security

`/embed/service-status` alone is anonymous and permits arbitrary website framing via CSP `frame-ancestors *`; no X-Frame-Options header may be injected on this route by proxies/platforms. Other Web responses use same-origin framing protection and existing authentication remains intact. Widget data/error documents are no-store and use only self-hosted scripts/styles, with text-only HTML-encoded names/messages. Error and rate-limit pages remain lightweight HTML and continue to refresh. No cookies or parent-page code are required. A parent website must allow the widget origin in its own CSP frame-src. Test public ingress without signing into the admin app and check CSP on the final proxied response. API CORS is unrelated to iframe framing and is not enabled.

## Embed a widget

Embed all active services for a type using this HTML example (replace the hostname with the public Web host and use its configured PathBase):

```html
<iframe
  src="https://status.example.org/closed-services/embed/service-status?ServiceType=Libraries&amp;ShowMessages=true"
  title="Library service status"
  width="100%"
  height="500"
  style="border: 0">
</iframe>
```

An individual service uses `ServiceID={guid}` instead of `ServiceType`. Include both when the ID must belong to a particular type; both filters are applied, not prioritized. At least one filter is required. `ShowMessages` accepts `true` or `false` and defaults to `false`. Filter names are case-insensitive. Conflicting repeated values are rejected. URL-encode type names, for example `ServiceType=Leisure%20centres`; HTML-escape query separators as `&amp;` inside the iframe attribute. The default Web PathBase is `/closed-services`, while the API defaults to the host root.

The standalone page includes current and upcoming disruptions, public closure reasons, and optional messages, including services with no current disruption. Dates use Europe/London with GMT/BST labels. It has no Blazor circuit, framework scripts, admin layout, parent-page script, or login requirement. The iframe document reloads every 60 seconds with the same URL and parameters, and displays the evaluation time and a manual refresh link. Browser background-tab throttling may delay timers. If JavaScript is disabled, use the manual refresh link. A full-document refresh may reset iframe scroll position/focus; it is intentionally kept simple for this first implementation.

Use an explicit iframe title and a height sufficient for the chosen content. Long lists scroll inside the iframe. Parent websites cannot directly measure a cross-origin iframe's content height; automatic sizing would require an optional parent script and an origin-validated postMessage protocol, not included in this minimal embed. No arbitrary CSS, HTML, or URL-based theming inputs are accepted.

## Validation and deployment checklist

Automated .NET tests cover public publication fields, active-service filtering, type/ID intersections, current/upcoming/expired/overlapping/indefinite closures, exact time boundaries, anonymous HTTP requests, error formats, rate limits, cache policies, framing scope, encoded messages, subpath assets/refresh URLs, and absence of Blazor assets. Run `dotnet test ClosedServices-Admin.Tests/ClosedServices-Admin.Tests.csproj` and `node --test ClosedServices-Admin.Tests/widget-refresh.test.cjs` (Node's built-in runner; no npm dependencies).

Before release:

1. Deploy and smoke-test the API first against PostgreSQL using the migration-service schema and a read-only credential; verify individual and service-type results and JSON errors.
2. Review messages/reason names for public suitability before exposing either surface.
3. Deploy Web with anonymous access to the widget route; do not put the entire application behind an external authentication gate that overrides endpoint metadata.
4. Load an iframe from a genuinely different HTTPS origin without signing in or using admin cookies. Confirm the final response has widget CSP and no conflicting frame headers, including those added by an ingress/proxy. Confirm admin pages remain protected and non-embeddable cross-origin.
5. Observe the iframe reload after a minute and preserve parameters, create/remove a closure, and verify next-refresh changes and scheduled start/end transitions. Check recovery from 429 and temporary backend failures.
6. Check keyboard/manual-refresh behavior, screen-reader headings/text, zoom, narrow widths, iframe scrolling, and browser background-tab behavior. Full refresh is not a live-region update.

PostgreSQL-backed checks, real cross-origin browser/accessibility checks, and deployed public-ingress/header checks require the hosting environment and remain release checks; HTTP test-host assertions and timer-unit tests do not replace those checks.
