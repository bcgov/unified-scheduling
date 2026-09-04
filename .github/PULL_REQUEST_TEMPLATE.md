# Pull Request for JIRA Ticket: ----**put ticket number + link here**----

## Developer Description

Please include a summary of the changes and the related issue. Please also include relevant motivation and context. List any dependencies that are required for this change.

Fixes # (issue)

## Type of change

Please delete options that are not relevant.

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] This change requires a documentation update

## Screenshots and Tests

Include UI screenshots or passing test results.

## Checklist:

- [ ] I have performed a self-review of my code
- [ ] I have commented my code, particularly in hard-to-understand areas
- [ ] I have added tests that prove my fix is effective or that my feature works
- [ ] New and existing unit tests pass locally with my changes
- [ ] Any dependent changes have been merged and published in downstream modules

## Deployment Dependencies (GitOps)

This app is deployed via ArgoCD from `bcgov-c/tenant-gitops-cb6495`. Changes in this repo
may require a matching PR in the GitOps repo under `services/unified-scheduling/overlays/{dev,test,prod}/`.

Check all that apply:

- [ ] No GitOps changes required
- [ ] **Feature flag enabled/disabled** -- update the API ConfigMap (`ua-configmap.yaml`) in each overlay to add/change `FeatureFlags__<Module>__Enabled`
- [ ] **New seed data set added** -- update the seeders ConfigMap (`ua-seeders-configmap.yaml`) in each overlay to add the new `SeedData__DataSets__N` entry
- [ ] **New environment variable or config value** -- update the API deployment patch or ConfigMap in each overlay
- [ ] **Network/infrastructure change** -- add or update Service, NetworkPolicy, or other resources in `base/resources/`
- [ ] GitOps PR: (link)

## Documentation References

Put any doc references here
