# Utility anomaly photos (T-009.3)

UtilityPhotoUploadService.SaveAndPersistAsync accepts one to three JPG/JPEG/PNG files, at most 5 MiB each. It checks extension, MIME type, image signature, declared length and actual streamed length before writing any files. Filenames are generated GUIDs; client filenames never become storage paths. Signature validation is not full image decoding.

The service reuses IPhotoStorage/LocalPhotoStorage. New private photos live under App_Data/report-photos outside wwwroot, with application URLs under /api/report-photos/. Existing general-report public uploads remain compatible.

The persistence callback receives the generated URLs. The caller must attach them to Report.PhotoPaths (and optionally ImageUrl), save the report and commit its transaction before returning. Validation errors raise UtilityPhotoValidationException. Storage, persistence and cancellation failures remove files saved by that invocation; cleanup uses a non-cancelled token and logs failures without replacing the original exception. A process crash cannot be compensated by this in-process callback.

GET /api/report-photos/{generated-name} requires authentication and a Resident, Crew, Dispatcher, Official or CityOfficial role. Residents can read their own report photos, crew can read assigned report photos, and dispatchers/officials can read report photos. Unassociated, missing and inaccessible photos return 404. The response uses a server-selected image content type, nosniff and no-store. No upload endpoint is introduced here; T-009.4 connects this service to utility report submission.

Verification: UtilityPhotoUploadTests covers file counts, size boundaries, mismatched formats, safe storage names, persistence/cancellation cleanup, partial batch storage failure, traversal rejection, and database-backed owner/crew/staff access checks. Existing regression tests also run.
