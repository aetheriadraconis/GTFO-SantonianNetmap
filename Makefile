SOLUTION = SantonianNetmap
PROFILE = Test
BEPINEX = /data/mods/R2ModMan/data/GTFO/profiles/$(PROFILE)/BepInEx
OUT = $(BEPINEX)/plugins/Aetheria-$(SOLUTION)

TARGET = $(OUT)/$(SOLUTION).dll

DOTNET = dotnet

SRC := Plugin.cs \
       Logger.cs

$(TARGET): $(SRC)
	$(DOTNET) build $(SOLUTION).sln

.PHONY: all, clean

all: $(TARGET)

clean:
	$(DOTNET) clean $(SOLUTION).sln
	rm -rf $(TARGET)
	rm -rf obj

