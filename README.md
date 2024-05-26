Div tooling for OpenAPI.NET
Early experimentation stage

* Define a composite specification from a number of OA docs + a config file
* Use said spec to create a new composite OpenAPI document
* TODO: Split a DTO into separate payloads based on said spec
* Validate a DTO based on OAS (must already exist as standalone functionality in some lib, maybe I'm using incorrect terminology when searching...)

Future experiments:
* Rules engine for Composite and Saga patterns
  * e.g. GET from composite endpoint first calls GET on endpoint 1, then uses some of response values as input for endpoint 2
* Orchestration
  * Validation step: first do a "dry run" of operations, calling a "validation" version of each endpoint
  * Rollback: each endpoint must provide a rollback endpoint.
