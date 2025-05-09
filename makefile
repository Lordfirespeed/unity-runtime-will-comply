.PHONY: default release build-tools release-thunderstore

# the first target is run by default, so make an empty target
default: ;

release:
	$(MAKE) build-tools
	$(MAKE) release-thunderstore

build-tools:
	dotnet build -c BuildTools

release-thunderstore:
	dotnet build -c ReleaseThunderstore
