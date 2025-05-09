.PHONY: default debug debug-tools release release-tools release-thunderstore

# the first target is run by default, so make an empty target
default: ;

debug:
	$(MAKE) debug-tools
	$(MAKE) release-thunderstore

release:
	$(MAKE) release-tools
	$(MAKE) release-thunderstore

debug-tools:
	dotnet build -c DebugTools

release-tools:
	dotnet build -c ReleaseTools

release-thunderstore:
	dotnet build -c ReleaseThunderstore
