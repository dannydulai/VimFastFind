-----------------------------------------------------------------------------------------
-----------------------------------------------------------------------------------------
--
-- Platform/Arch toolchain configuration
-- 
-----------------------------------------------------------------------------------------
-----------------------------------------------------------------------------------------

compilers.Mono_CSC = "dmcs"
compilers.ScalaC = "fsc"

--print ("BUILDING FOR ARCHITECTURE       " .. arch.get())
--print ("BUILDING FOR PLATFORM           " .. platform.get())

if arch.is('arm') then
    defines { "ARCH_ARM" }
elseif arch.is('x86') then
    defines { "ARCH_X86" }
elseif arch.is('x64') then
    defines { "ARCH_X64" }
else
    error("unknown architecture");
end

if platform.is('windows') then
    if arch.is('x86') then
        defines { "PLATFORM_WINDOWS" }
        if (os.isdir("c:\\Program Files (x86)\\Microsoft Visual Studio 10.0")) then
            VSROOT = "c:\\Program Files (x86)\\Microsoft Visual Studio 10.0";
            os.setenv("PATH", "c:\\Program Files (x86)\\Microsoft Visual Studio 10\\Common7\\IDE:" .. os.getenv("PATH"))
            includedirs { "c:\\Program Files (x86)\\Microsoft SDKs\\Windows\\v7.0A\\Include" }
            libdirs { "c:\\Program Files (x86)\\Microsoft SDKs\\Windows\\v7.0A\\lib" }
        elseif (os.isdir("c:\\Program Files\\Microsoft Visual Studio 10.0")) then
            VSROOT = "c:\\Program Files\\Microsoft Visual Studio 10.0";
            includedirs { "c:\\Program Files\\Microsoft SDKs\\Windows\\v7.0A\\Include" }
            libdirs { "c:\\Program Files\\Microsoft SDKs\\Windows\\v7.0A\\lib" }
        else
            error("you don't seem to have the microsoft platform sdk or visual studio installed.");
        end

        libdirs { VSROOT .. "\\VC\\lib" }
        includedirs { VSROOT .. "\\VC\\include" }

        compilers.MSNET_CSC = "c:\\WINDOWS\\Microsoft.NET\\Framework\\v4.0.30319\\csc.exe"
        compilers.MSNET_CSC_DEFINES = { "MSNET" }
        compilers.MSNET_RESGEN = "c:\\Program Files\\Microsoft SDKs\\Windows\\v6.0A\\bin\\resgen.exe"

        compilers.MSNET_FSC = "C:\\Program Files (x86)\\Microsoft F\\#\\v4.0\\Fsc.exe"
        compilers.MSNET_FSC_DEFINES = { "MSNET" }

        compilers.MS_CC = VSROOT .. "\\vc\\bin\\cl.exe"
        compilers.MS_CXX = VSROOT .. "\\vc\\bin\\cl.exe"
        compilers.MS_SHAREDLINK = VSROOT .. "\\vc\\bin\\link.exe"
        compilers.MS_STATICLINK = VSROOT .. "\\vc\\bin\\lib.exe"

        compilers.EDITBIN = VSROOT .. "\\VC\\bin\\editbin"

        compilers.MSNET_CSC_FLAGS = "-platform:x86"
    else
        error('unsupported windows architecture')
    end
end

if platform.is('macosx') then
    defines { "PLATFORM_MACOSX", "PLATFORM_OSX" }
    compilers.Mono_CSC_DEFINES = { "MONO" }
    compilers.MSNET_CSC_DEFINES = { "MONO" }
    compilers.Mono_CSC_FLAGS  = "-platform:x86"
    compilers.Mono_FSC = "/Library/Frameworks/Mono.framework/Versions/Current/bin/fsc"
    compilers.Mono_FSC_DEFINES = { "MONO" }
    compilers.GCC_AR = 'libtool'
    compilers.GCC_AR_FLAGS = "-static -o"

    if arch.is('x86') then
        compilers.GCC_CC_FLAGS = '-m32 -mmacosx-version-min=10.5'
        compilers.GCC_CXX_FLAGS = '-m32 -mmacosx-version-min=10.5'
    else
        error('unsupported macosx architecture')
    end
end

if platform.is('linux') then
    compilers.Mono_CSC_DEFINES = { "MONO" }
    compilers.MSNET_CSC_DEFINES = { "MONO" }
    defines { "PLATFORM_LINUX" }
end
