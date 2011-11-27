solution "VimFastFindRoot"
    dofile('genmake.os.lua')

    flags { "ExtraWarnings" }

    HAVE_MONO_POSIX         = platform.is('macosx') or (platform.is('linux') and not platform.is('android') and not platform.is('webos'))

    if HAVE_MONO_POSIX         then defines "HAVE_MONO_POSIX"         end

    --defines "PERFTIMERS"

    configuration "debug"
        defines "DEBUG"
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

        linksystemlibs "System"

        if (platform.is("macosx")) then
            linksystemlibs "Mono.Posix"
        end

        flags { "Unsafe" }

        compilefiles {
            "server.cs",
            "utils.cs"
        }

    done "VFFServer"

done "VimFastFindRoot"
