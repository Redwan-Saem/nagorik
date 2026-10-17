# Configurable utility routing (T-009.2)

UtilityRoutingService is registered as a scoped service. ResolveAsync accepts an anomaly type, optional area ID, optional latitude/longitude pair, and cancellation token. It reads current UtilityRoutingRules rows without tracking or caching, so saved activation, priority, bounds, and provider changes take effect on the next resolution.

## Rule schema

| Field | Meaning |
|---|---|
| Name | Required unique rule name; nonblank, at most 160 characters including padding. |
| Utility | Concrete provider: Desco, Dpdc, WaterAuthority, or GasAuthority. Unassigned is an outcome, not a valid rule target. |
| AnomalyType | Exact anomaly type, or null to match any defined type. |
| AreaId | Exact ReportAreas ID, or null for any/no selected area. Referenced areas cannot be deleted while a rule uses them. |
| MinLatitude / MaxLatitude / MinLongitude / MaxLongitude | Either all null, or all present with valid ranges and minimum <= maximum. Edges are inclusive. |
| Priority | Integer from 0 to 10,000; higher values take precedence. |
| IsActive | Defaults to false. Only active rules participate. |

All specified criteria are combined with AND. A rule with both an area and bounds requires both to match; it does not infer area membership from coordinates. A bounded rule cannot match when GPS is missing. Bounds do not cross the antimeridian; represent such coverage as separate configured rules if needed.

Indexes cover the unique rule name, area FK, and active/type/area/priority filtering. Database checks enforce valid provider/type names, bounds, priority, name length, and boolean active values.

## Deterministic selection

Candidates are filtered in the database, then ranked by:

1. Higher Priority.
2. An explicit AreaId before a wildcard area.
3. Coordinate bounds before no bounds.
4. An exact anomaly type before a wildcard type.

Priority is deliberately first, so an explicitly higher-priority broad rule can override a more specific lower-priority rule.

If the highest-ranked rules all select the same provider, the service returns Matched and chooses the lowest rule ID. MatchingRuleIds lists every rule tied at that highest rank, in ascending ID order. If those rules select different providers, the result is Ambiguous with UtilityProvider.Unassigned and no selected RuleId. It does not guess a provider from insertion order.

With no matching active rules, the result is NoMatch with Unassigned. Undefined anomaly types, nonpositive or nonexistent area IDs, incomplete GPS pairs, nonfinite coordinates, and out-of-range coordinates produce InvalidInput. Cancellation is observed and propagated. NoMatch and Ambiguous are explicit triage results; they must not be represented as confirmed utility delivery.

The service only returns a routing decision. It does not change reports, send notifications, expose an admin API, or contact a provider. T-009.4 will call it from the authenticated submission flow.

## Initial seed data

Migration 20261017030335_AddUtilityRoutingRules seeds 20 rules:

- ID 1: WaterLeak to the generic WaterAuthority identifier, active, priority 0, no area/bounds.
- ID 2: GasLeak to the generic GasAuthority identifier, active, priority 0, no area/bounds.
- IDs 10-18: inactive DESCO templates for the three existing area IDs and ExposedWire/FallenPole/PowerOutage, priority 100.
- IDs 19-27: equivalent inactive DPDC templates.

The water/gas defaults classify provider categories only; they do not identify a verified agency, service boundary, or delivery integration. No verified DESCO/DPDC coverage data was supplied. Electricity templates are explicitly named unverified and remain inactive, with no invented coordinate boundaries. Electrical anomalies therefore resolve to NoMatch until verified configuration is activated.

To configure a route, inspect the rule and its area/type, obtain verified coverage, set the intended provider and any necessary coordinate bounds, choose the priority, and activate it through a trusted database administration path. Use the persisted provider names Desco and Dpdc, not uppercase display labels. Do not enable both provider templates at the same rank for the same criteria unless the resulting Ambiguous outcome is intended.

For example, an administrator can activate a verified rule by its ID with a parameterized UPDATE:

    UPDATE UtilityRoutingRules
    SET IsActive = 1, Priority = @VerifiedPriority
    WHERE Id = @VerifiedRuleId;

Supply only a rule ID and priority that have been reviewed against verified coverage. The application does not execute this example or activate unverified templates.

Seed insertion occurs once through migration. Calling database update again does not overwrite later configuration edits or add duplicate rules. The existing report category-to-Authority mapping is unchanged.

## Migration and tests

Apply:

    dotnet ef database update

Rollback:

    dotnet ef database update 20261016221457_AddUtilityAnomalySchema

The migration creates the rule table, constraints, indexes, FK, and seeds; it does not rebuild or change Reports. Rollback drops the routing table and its configuration, leaving reports and utility anomaly metadata intact. Reapplying creates the initial defaults again, so export verified configuration before rollback if it must be retained.

The task adds tests for default routes, inactive electricity templates, activation/deactivation without restart, inclusive rectangle edges, area/type intersection, missing GPS, all ranking levels, conflicting-provider ambiguity, same-provider ties, current database reads, invalid input, cancellation, constraints, seed stability, report preservation, and migration rollback/reapply.
