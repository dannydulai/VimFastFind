solution "VimFastFindRoot"
    dofile('genmake.os.lua')

    flags { "ExtraWarnings" }

    HAVE_MONO_POSIX         = platform.is('macosx') or (platform.is('linux') and not platform.is('android') and not platform.is('webos'))

    if HAVE_MONO_POSIX         then defines "HAVE_MONO_POSIX"         end

    --defines "PERFTIMERS"

    configuration "debug"
        defines "DEBUG"
        defines "TRACE"
        flags "Symbols"
        targetdir "bin/debug"
        objectsdir "obj/debug"
    done "debug"

    configuration "release"
        defines "NDEBUG"
        flags "Optimize"
        targetdir "bin/release"
        objectsdir "obj/release"
    done "release"
        
-- ----------------------------------------------

    project "VFFServer"
        category "Tools"
        kind "ConsoleApp"
        language "C#"

        linksystemlibs {
            "System",
            "System.Core",
            "Mono.Posix",
        }

        flags { "Unsafe" }

        compilefiles {
            "server.cs",
            "utils.cs",
            "DirectoryWatcher.cs",
            "VolumeWatcher.cs",
        }

    if platform.is('windows') then
        compilefiles {
            "DriveDetector.cs",
            "win_utils.cs",
        }
        copyprojects "storagestringutils"
    elseif platform.is('macosx') then
        compilefiles "osx_utils.cs"
        linkfiles "MonoMac"
    end

    done "VFFServer"

    if platform.is('windows') then
        project "storagestringutils"
            category "native"
            kind "SharedLib"
            language "C"
            includedirs "."
            compilefiles "storage_stringutils.c"
        done "storagestringutils"
    end


done "VimFastFindRoot"
