Tags serve as the trigger for actions.
Actions (tag assignment) must be running in a way that ensures the release occurs last, e.g.:
```
vX.Y.Z-core-nuget
vX.Y.Z-linux-x64
vX.Y.Z-osx-x64
vX.Y.Z-win-x64
vX.Y.Z // github release tag
```

If a tag is deleted on the origin, it will remain locally (which might cause issues). To remove it, use:
```sh
git tag -d <tag_name>
```
