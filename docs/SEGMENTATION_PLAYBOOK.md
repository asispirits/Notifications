# Segmentation Playbook

This playbook defines a practical segmentation model for Notifications using the Beamer widget.

## Goals

- Send messages to all endpoints, groups, or specific locations.
- Keep segment names predictable and easy to operate.
- Avoid code changes when adding or moving endpoints.

## Segment Taxonomy

Use a small fixed set of segment dimensions:

- `tenant:<name>` - company/group level
- `brand:<name>` - brand/business unit
- `region:<name>` - geographic area
- `market:<name>` - optional sub-region
- `site:<id>` - physical location/store/office
- `role:<name>` - endpoint role (cashier, manager, warehouse, etc.)
- `env:<name>` - prod/stage/dev
- `device:<id>` - optional direct endpoint targeting
- `user:<id>` - optional direct user targeting

## Naming Rules

- Lowercase only.
- Use `:` between key/value.
- Use `-` for separators inside values.
- No spaces.

Examples:

- `brand:spirits`
- `region:east`
- `site:store-104`
- `role:cashier`
- `env:prod`

## Recommended Endpoint Profile

For each endpoint, set:

- Stable `UserId`
- `SegmentFilters` with 4-7 tags
- `IncludeViewerUserIdSegment=false` unless you need per-user targeting

Example:

```json
{
  "UserId": "store-104-pos-01",
  "SegmentFilters": "tenant:asi;brand:spirits;region:east;site:store-104;role:cashier;env:prod",
  "IncludeViewerUserIdSegment": false
}
```

## Targeting Patterns In Beamer

Use Beamer post audience filters to match one or more segment tags.

Examples:

1. All production endpoints:
   - `env:prod`
2. Only east region:
   - `region:east`
3. One site only:
   - `site:store-104`
4. All managers:
   - `role:manager`
5. Site managers only:
   - `site:store-104` and `role:manager`
6. One endpoint (direct):
   - `device:store-104-pos-01`

## Operations Workflow

1. Assign each endpoint a `UserId` and `SegmentFilters`.
2. Deploy the endpoint config.
3. In Beamer, create a post and set the audience to matching segments.
4. Publish and validate on one matching and one non-matching endpoint.
5. Keep a segment inventory CSV under source control.

## Suggested CSV Inventory Columns

- `endpoint_name`
- `user_id`
- `tenant`
- `brand`
- `region`
- `market`
- `site`
- `role`
- `env`
- `extra_segments`

## Rollout Checklist

1. Define segment dictionary and approved values.
2. Generate endpoint configs from the inventory.
3. Deploy to pilot endpoints.
4. Send test campaigns by `site`, `region`, and `role`.
5. Validate and roll out globally.
