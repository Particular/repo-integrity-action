# repo-integrity-action

This action tests the structure of Particular Software code repos, focusing heavily on conventions inside project files.

Some tests include:

* PackageReference, ProjectReference, and other item types should not be mixed within the same ItemGroup
* Non-prerelease packages cannot have prerelease dependencies
* .NET versions in workflows and test projects should match up
* Workflows containing `run` steps should declare defaults for shell (`pwsh` or `bash`) so that behavior is consistent on Windows and Linux

Some tests will also behave differently when executed as part of a release workflow, for instance, to prevent an RTM package from being built using prerelease dependencies.

The tests within the action are implemented as NUnit tests and will output a test summary in the GitHub Actions Summary view the same as a unit test project.

## Usage

This is how to use the action directly:

```yaml
      - name: Repo integrity tests
        uses: Particular/repo-integrity-action@main
```

The tests are invoked by the action using `dotnet run` on a project inside this action repo. The test project is built using a .NET version known to be preinstalled on GitHub Actions runners, so it isn't necessary to use `actions/setup-dotnet`.

But more generally, repos will execute this workflow as part of the [shared workflow](https://github.com/Particular/shared-workflows/blob/main/.github/workflows/code-analysis.yml) which provides additional capabilities.

## Warnings

New rules should be added using the `Warn(…)` method rather than `Fail(…)` so they can be rolled out to all repos before making them required to merge a PR.

When used through 

Tests that create warnings will not fail the test, and the workflow will be green as well, but will append to the GitHub Actions markdown summary, and then that summary will be uploaded as a run artifact. Internal automations will note the presence of the artifact wiht the known name and log the workflow result as a warning rather than success or failure.

Once the warnings have been verified to be addressed in all repos and branches, the method usage can be changed from `Warn(…)` to `Fail(…)` to make the check required for merging PRs.

The uploading of artifacts to make warnings work is handled by the [shared workflow](https://github.com/Particular/shared-workflows/blob/main/.github/workflows/code-analysis.yml) and not directly in the action.

## Ignores

There are some valid cases for creating an exception to a rule, but this should be a last resort, and never used just to get the test to be quiet. First, consider if the issue is fixable. Second, consider whether the test can be updated to account for an edge case that should be allowed. Only after these avenues are exhuasted should an ignore be added, and in most cases, only on a release branch that will go away after some time.

To create an ignore, create a file `.repointegrity.yml` in the root of the repo [like this one](https://github.com/Particular/ParticularTemplates/blob/master/.repointegrity.yml) containing the following:

```yaml
ignore:
  # Comment with the justification for ignoring the test
  - test: TestName
    path: src/PathTo/OffendingFile.extension
```

* `test` is the method name of the test (the method decorated by the `[Test]` attribute) in the [test project](https://github.com/Particular/repo-integrity-action/tree/main/src/RepoIntegrityTests)
* `path` is the path to the offending file identified in the test output. Paths use forward slashes regardless of host OS.
  * Some wildcarding is allowed (`*` for any character within a file/directory name, `**` to match any/all characters) but try to be conservative and avoid wildcarding if possible.
  * The [serializer compatibility tests](https://github.com/Particular/NServiceBus.Serializers.CompatTests/blob/master/.repointegrity.yml) are a good reason to use wildcards, as each new minor version of NServiceBus would create a new offending path.

## License

The scripts and documentation in this project are released under the [MIT License](LICENSE.md).
