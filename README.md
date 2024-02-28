# repo-integrity-action

This action tests the structure of Particular Software code repos, focusing heavily on conventions inside project files.

Some tests include:

* PackageReference, ProjectReference, and other item types should not be mixed within the same ItemGroup
* Test projects should use absolute versions of dependencies so that Dependabot can update them
* Non-prerelease packages cannot have prerelease dependencies
* Ensure component dependency ranges have absolute version in test projects

Some tests will also behave differently when executed as part of a release workflow, for instance, to prevent an RTM package from being built using prerelease dependencies.

The tests within the action are implemented as NUnit tests and will output a test summary in the GitHub Actions Summary view the same as a unit test project.

## Usage

In a CI workflow, add a job to the end of the workflow. This ensures that the integrity tests do not need to be executed on every single job.

```yaml
  repo-integrity:
    name: Repo integrity
    runs-on: ubuntu-22.04
    steps:
      - name: Checkout
        uses: actions/checkout@v4.1.1
      - name: Repo integrity tests
        uses: Particular/repo-integrity-action@v1.0.0
```

In a release workflow, add a step before the Build step:

```yaml
      - name: Repo integrity tests
        uses: Particular/repo-integrity-action@main
```

The tests are invoked by the action using `dotnet run` on a project inside this action repo. The test project is built using a .NET version known to be preinstalled on GitHub Actions runners, so it isn't necessary to use `actions/setup-dotnet`.

## License

The scripts and documentation in this project are released under the [MIT License](LICENSE.md).
