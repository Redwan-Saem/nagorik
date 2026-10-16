# Official report list (T-008.2)

The official list screen is `/official/reports`, linked as City reports in official navigation. It loads the paged API with cookies, displays category/area/status/urgency and UTC creation dates, and supports sortable headers and Previous/Next controls. Paging preserves query filters; sorting resets to page 1. Navigation updates the browser URL, and browser Back/Forward restores the requested state. Empty, loading, connection failure/retry, invalid-query reset, and permission states are explicit. Expired API sessions navigate to login with a return URL. User-provided report text is rendered with DOM textContent.

Category checkboxes come from `ReportLookups.Categories`; the area dropdown reads the current `ReportAreas` table after the official role check. Choose multiple categories and one area, then select **Apply filters**. Unchecked categories and **All areas** mean no restriction. Apply resets to page 1 and preserves sorting. **Clear filters** removes category and area parameters, resets to page 1, and preserves sorting. Paging, sorting, refresh, and Back/Forward retain applied selections. Draft changes do not fetch until Apply. The URL updates when navigation starts, so failed requests retain the selected state for refresh or Retry. Unknown area IDs remain visibly selected until cleared.

The result count uses the API's filtered `totalCount`, not the current page size. Zero matches display **No reports match your filters.** with the table and pagination hidden. Checkbox labels, a category fieldset, and named Apply/Clear controls support keyboard navigation and narrow screens.

`GET /api/reports` accepts authenticated `Official` and `CityOfficial` roles. `Official` remains the existing project role; `CityOfficial` is an alias on the new official reporting routes. Existing resident creation/detail endpoints and zone-map role rules are unchanged. Development startup seeds `official1@test.com` with role `Official` and the existing development password `Test@12345`.

Example:

```text
/api/reports?page=1&categories=RoadDamage&categories=WaterLogging&areaId=1&sort=urgency&direction=desc
```

| Query | Default | Accepted values |
|---|---|---|
| `page` | `1` | Positive page number, at most 107374182 to keep the offset within Int32. |
| `categories` | All | Repeated category names from ReportCategory; case-insensitive, with at most seven entries. |
| `areaId` | All | Positive ReportAreas ID. An unknown ID produces an empty result. |
| `sort` | `date` | `date`, `status`, `urgency`. |
| `direction` | `desc` | `asc`, `desc`. |

Categories use OR within the selected group and AND with area. Page size is fixed at 20. Sorting uses UTC CreatedAt, alphabetical persisted status names, or numeric urgency priority (Low, Normal, High, Critical). Date ties use Id in the selected direction. Status/urgency ties use newest CreatedAt and descending Id.

The response contains `items`, `page`, `pageSize`, `totalCount`, and `totalPages`. Each item contains `id`, `code`, `title`, `category`, `areaId`, `areaName`, `status`, `urgency`, `location`, and `createdAt`. Unclassified areas have null `areaId` and `areaName`. Empty matches return empty items and zero counts/pages; a page beyond the end returns empty items with the filtered total preserved.

The database applies filters, count, order, offset, and limit. The endpoint does not load the full report table into memory. Invalid query values return 400, anonymous calls return 401, and other authenticated roles return 403. Responses are not cached.
