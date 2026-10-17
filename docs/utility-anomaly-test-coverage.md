# Utility anomaly regression and browser E2E (T-009.9)

## Run

From the repository root:

    dotnet test .\Nagorik.Tests\Nagorik.Tests.csproj --no-restore
    node --test (Get-ChildItem -LiteralPath ClientTests -Filter '*.test.cjs' | Select-Object -ExpandProperty FullName)
    dotnet test .\Nagorik.UiTests\Nagorik.UiTests.csproj --filter FullyQualifiedName~UtilityAnomalyE2eTests

The browser suite uses the existing Selenium packages and installed Chrome in headless mode. Selenium Manager obtains a matching driver when necessary. No application server, production database, credentials, browser profile or NAGORIK_URL setting is required. The older ServiceWorkerTests suite uses a separate external-server fixture and is not part of this filtered utility E2E run.

## Coverage

| Behavior | Coverage |
| --- | --- |
| Schema, enums and high-risk urgency | UtilityAnomalySchemaTests, submission tests for all six anomaly types |
| Routing precedence, bounds, inactive rules, ambiguity and fallbacks | UtilityRoutingServiceTests |
| Photo count, type/signature, size, unsafe filenames, cleanup and authorized reads | UtilityPhotoUploadTests and submission HTTP tests |
| Exactly three 5 MiB files accepted; three files one byte over the individual limit rejected without writes | ThreePhotosRespectExactPerFileLimitAtHttpBoundary |
| Required fields, description edges, coordinates and authenticated identity | UtilityAnomalySubmissionApiTests |
| Reference uniqueness and quota concurrency | Concurrent requests using separate SQLite connections |
| Ten accepted per rolling hour and Retry-After | Hour boundary tests, including historical counts above ten |
| Duplicate warnings and 24-hour edges | Owner/type/area/address tests, Unicode/whitespace normalization unit cases |
| Owner-only list, reference links, status labels, pagination, confirmation reload | Resident API/page integration tests |
| Form behavior, previews, GPS, double clicks, cooldown and API failure messages | ClientTests/utility-anomaly-form.test.cjs |
| Real resident form submission through official dashboard | UtilityAnomalyE2eTests at desktop and 390-pixel mobile viewports |

## Browser flow and isolation

Each browser case starts a loopback-only ASP.NET host using the real controllers, Razor views, CSS/JavaScript, JWT cookie authentication, routing/submission/photo services and EF migrations. It creates a unique temporary SQLite file and private photo directory, and seeds only synthetic fixture accounts. No report is written to the normal nagorik.db. The fixture stops the host/browser and deletes its temporary storage on disposal.

The browser signs in through login.html and AuthController, selects Gas leak and a real seeded area, enters an address/description, chooses a valid PNG, waits for preview validation, and submits the form. It checks the UA reference after reload, verifies Submitted on My Reports, and signs in as resident2 to confirm isolation. It then signs in as Official (desktop) or CityOfficial (mobile), filters Utility Anomaly and verifies the newly submitted report with High urgency. Database assertions confirm the same reference, resident, routed GasAuthority provider, photo and initial status history.

Native WebDriver clicks remain in use; controls are scrolled into view instantly before clicking to avoid races with the application's smooth scrolling. The mobile case uses Chrome device metrics at 390 pixels. No application behavior is mocked or replaced in this browser flow.

Screenshots are saved under obj/utility-e2e (ignored by Git), with failure screenshots when an assertion fails. These screenshots are evidence artifacts, not production assets.

## Verified result

521 .NET regression tests passed, 96 JavaScript tests passed, and both browser E2E cases passed using Chrome 155 on Windows. Browser cases exercise real login, multipart upload, persisted confirmation, ownership and dashboard rendering; the HTTP/unit suites cover the broader validation, concurrency and boundary matrix.
