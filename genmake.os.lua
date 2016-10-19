-----------------------------------------------------------------------------------------
-----------------------------------------------------------------------------------------
--
-- Platform/Arch toolchain configuration
--
-----------------------------------------------------------------------------------------
-----------------------------------------------------------------------------------------

local monopath = os.getenv('MONOROOT')

if (monopath) then
    compilers.Mono_MONO = monopath .. "/bin/mono-sgen"
    compilers.Mono_CSC = monopath .. "/bin/mcs"
    compilers.Mono_FSC = monopath .. "/bin/fsharpc"
else
    compilers.Mono_MONO = "mono-sgen"
    compilers.Mono_CSC = "mcs"
    compilers.Mono_FSC = "fsharpc"
end
compilers.ScalaC = "scalac"

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

--
-- paths are set up in our cygwin environment to point to SDK assets
--
-- in order to actually pass these paths into tools, we need to translate
-- these into windows paths.
--
-- This is non-trivial, since it requires resolving drive letters, following
-- cygwin symlinks, etc, so we let cygpath do the heavy lifting.
--
-- This routine is slow, so avoid calling it more than once for the same path
--
function translate_path_for_tool(p)
    if os.is('windows') then
        os.rmfile('.genmake_path_tmp')
        local ok,_,_ = os.execute("cygpath -wa '" .. p .. "' > .genmake_path_tmp")
        if not ok then
            error("error executing cygpath -wa '" .. p .. "'")
        end
        local f = assert(io.open('.genmake_path_tmp', 'rt'))
        for line in f:lines() do
            f:close()
            os.rmfile('.genmake_path_tmp')
            --print ('translated path [' .. p .. '] ===> [' .. line .. ']')
            return line
        end
        os.rmfile('.genmake_path_tmp')
        error('failed to read line from cygpath')
    end
    return p
end

if platform.is('windows') then
    if arch.is('x64') then
        defines { "PLATFORM_WINDOWS" }
        if (os.isdir("c:\\Program Files (x86)\\Microsoft Visual Studio 14.0")) then
            VSROOT = "c:\\Program Files (x86)\\Microsoft Visual Studio 14.0";
            os.setenv("PATH", "c:\\Program Files (x86)\\Microsoft Visual Studio 10\\Common7\\IDE:" .. os.getenv("PATH"))
        elseif (os.isdir("c:\\Program Files\\Microsoft Visual Studio 14.0")) then
            VSROOT = "c:\\Program Files\\Microsoft Visual Studio 14.0";
        else
            error("you don't seem to have the microsoft platform sdk or visual studio installed.");
        end

        libdirs { VSROOT .. "\\VC\\lib\\amd64" }
        includedirs { VSROOT .. "\\VC\\include" }

        includedirs { "c:\\Program Files (x86)\\Windows Kits\\8.1\\Include\\shared" } 
        includedirs { "c:\\Program Files (x86)\\Windows Kits\\8.1\\Include\\um" } 
        libdirs     { "c:\\Program Files (x86)\\Windows Kits\\8.1\\Lib\\winv6.3\\um\\x64" }
        includedirs { "c:\\Program Files (x86)\\Windows Kits\\10\\Include\\10.0.10240.0\\ucrt" }
        libdirs     { "c:\\Program Files (x86)\\Windows Kits\\10\\Lib\\10.0.10240.0\\ucrt\\x64" }

        compilers.MSNET_CSC = "c:\\WINDOWS\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe"
        compilers.MSNET_CSC_DEFINES = { "MSNET" }
        compilers.MSNET_RESGEN = "c:\\Program Files (x86)\\Microsoft SDKs\\Windows\\v7.0A\\bin\\resgen.exe"

        compilers.MSNET_FSC = "C:\\Program Files (x86)\\Microsoft SDKs\\F\\#\\4.0\\Framework\\v4.0\\Fsc.exe"
        compilers.MSNET_FSC_DEFINES = { "MSNET" }

        compilers.MS_CC = VSROOT .. "\\vc\\bin\\amd64\\cl.exe"
        compilers.MS_CXX = VSROOT .. "\\vc\\bin\\amd64\\cl.exe"
        compilers.MS_SHAREDLINK = VSROOT .. "\\vc\\bin\\amd64\\link.exe"
        compilers.MS_STATICLINK = VSROOT .. "\\vc\\bin\\amd64\\lib.exe"

        compilers.EDITBIN = VSROOT .. "\\VC\\bin\\amd64\\editbin"

        compilers.MSNET_CSC_FLAGS = "-platform:x64"
        compilers.MSNET_FSC_FLAGS = "--platform:x64 --mlcompatibility"

    elseif arch.is('x86') then
        defines { "PLATFORM_WINDOWS" }
        if (os.isdir("c:\\Program Files (x86)\\Microsoft Visual Studio 14.0")) then
            VSROOT      = "c:\\Program Files (x86)\\Microsoft Visual Studio 14.0";
            os.setenv("PATH", "c:\\Program Files (x86)\\Microsoft Visual Studio 10\\Common7\\IDE:" .. os.getenv("PATH"))
        elseif (os.isdir("c:\\Program Files\\Microsoft Visual Studio 14.0")) then
            VSROOT      = "c:\\Program Files\\Microsoft Visual Studio 14.0";
        else
            error("you don't seem to have the microsoft platform sdk or visual studio installed.");
        end

        includedirs { "c:\\Program Files (x86)\\Windows Kits\\8.1\\Include\\shared" } 
        includedirs { "c:\\Program Files (x86)\\Windows Kits\\8.1\\Include\\um" } 
        libdirs     { "c:\\Program Files (x86)\\Windows Kits\\8.1\\Lib\\winv6.3\\um\\x86" }
        includedirs { "c:\\Program Files (x86)\\Windows Kits\\10\\Include\\10.0.10240.0\\ucrt" }
        libdirs     { "c:\\Program Files (x86)\\Windows Kits\\10\\Lib\\10.0.10240.0\\ucrt\\x86" }

        libdirs     { VSROOT .. "\\VC\\lib" }
        includedirs { VSROOT .. "\\VC\\include" }

        compilers.MSNET_CSC         = "c:\\WINDOWS\\Microsoft.NET\\Framework\\v4.0.30319\\csc.exe"
        compilers.MSNET_CSC_DEFINES = { "MSNET" }
        compilers.MSNET_RESGEN      = "c:\\Program Files (x86)\\Microsoft SDKs\\Windows\\v7.0A\\bin\\resgen.exe"

        compilers.MSNET_FSC = "C:\\Program Files (x86)\\Microsoft SDKs\\F\\#\\4.0\\Framework\\v4.0\\Fsc.exe"
        compilers.MSNET_FSC_DEFINES = { "MSNET" }

        compilers.MS_CC         = VSROOT .. "\\vc\\bin\\cl.exe"
        compilers.MS_CXX        = VSROOT .. "\\vc\\bin\\cl.exe"
        compilers.MS_SHAREDLINK = VSROOT .. "\\vc\\bin\\link.exe"
        compilers.MS_STATICLINK = VSROOT .. "\\vc\\bin\\lib.exe"

        compilers.EDITBIN = VSROOT .. "\\VC\\bin\\editbin"

        compilers.MSNET_CSC_FLAGS = "-platform:x86"
        compilers.MSNET_FSC_FLAGS = "--platform:x86 --mlcompatibility"
    else
        error('unsupported windows architecture')
    end
end

if platform.is('macosx') then
    defines { "PLATFORM_MACOSX", "PLATFORM_OSX" }
    compilers.Mono_CSC_DEFINES  = { "MONO" }
    compilers.MSNET_CSC_DEFINES = { "MONO" }
    compilers.Mono_FSC_DEFINES  = { "MONO" }
    compilers.GCC_AR            = '/usr/bin/libtool'
    compilers.GCC_AR_FLAGS      = "-static -o"

    if arch.is('x86') then
        compilers.Mono_CSC_FLAGS = "-platform:x86 -noconfig"
        compilers.Mono_FSC_FLAGS = "--platform:x86 --mlcompatibility --nowarn:62"
        compilers.GCC_CC_FLAGS   = '-m32 -mmacosx-version-min=10.6'
        compilers.GCC_CXX_FLAGS  = '-m32 -mmacosx-version-min=10.6'
    elseif arch.is('x64') then
        compilers.Mono_CSC_FLAGS = "-platform:x64 -noconfig"
        compilers.Mono_FSC_FLAGS = "--platform:x64 --mlcompatibility --nowarn:62"
        compilers.GCC_CC_FLAGS   = '-mmacosx-version-min=10.6'
        compilers.GCC_CXX_FLAGS  = '-mmacosx-version-min=10.6'
        compilers.Mono_MONO      = 'mono64'
    else
        error('unsupported macosx architecture')
    end
end

if platform.is('ios') then
    defines { "PLATFORM_IOS" }
    compilers.Mono_CSC_DEFINES  = { "MONO" }
    compilers.MSNET_CSC_DEFINES = { "MONO" }
    -- this -lib param will make our managed stuff link against Xamarin System* libs instead of the standard mono MDK libs
    compilers.Mono_CSC_FLAGS    = "-platform:x86 -noconfig -lib:/Library/Frameworks/Xamarin.iOS.framework/Versions/Current/lib/mono/Xamarin.iOS"
    compilers.Mono_FSC_FLAGS    = "--platform:x86 --mlcompatibility --nowarn:62"
    compilers.GCC_CC            = '/Applications/Xcode.app/Contents/Developer/Toolchains/XcodeDefault.xctoolchain/usr/bin/clang'
    compilers.GCC_CXX           = '/Applications/Xcode.app/Contents/Developer/Toolchains/XcodeDefault.xctoolchain/usr/bin/clang++'
    compilers.GCC_AR            = '/Applications/Xcode.app/Contents/Developer/Platforms/iPhoneOS.platform/Developer/usr/bin/libtool'
    compilers.GCC_CC_FLAGS      = "-arch arm64 -miphoneos-version-min=8.3 " ..
                                  "-I/Applications/Xcode.app/Contents/Developer/Platforms/iPhoneOS.platform/Developer/SDKs/iPhoneOS.sdk/usr/include " ..
                                  "-isysroot /Applications/Xcode.app/Contents/Developer/Platforms/iPhoneOS.platform/Developer/SDKs/iPhoneOS.sdk"
    compilers.GCC_CXX_FLAGS     = compilers.GCC_CC_FLAGS

    compilers.Mono_FSC_DEFINES = { "MONO" }
    compilers.GCC_AR = 'libtool'
    compilers.GCC_AR_FLAGS = "-static -o"
end

if platform.is('android') then
    defines { "PLATFORM_ANDROID" }

    compilers.Mono_FSC_FLAGS  = "--mlcompatibility --nowarn:62"

    local android_sdk       = os.getenv('ANDROID_SDK')
    local android_ndk       = os.getenv('ANDROID_NDK')
    local android_toolchain = nil

    if arch.is('arm') then
        android_toolchain = os.getenv('ANDROID_TOOLCHAIN_ARM')
    elseif arch.is('x86') then
        android_toolchain = os.getenv('ANDROID_TOOLCHAIN_X86')
    end

    if (android_sdk == '')             then android_sdk = nil             end
    if (android_ndk == '')             then android_ndk = nil             end
    if (android_toolchain == '')       then android_toolchain = nil       end

    if (not android_sdk)             then error('You must set the $ANDROID_SDK environment variable to the location of the android sdk')                             end
    if (not android_ndk)             then error('You must set the $ANDROID_NDK environment variable to the location of the android ndk')                             end
    if arch.is('arm') then
        if (not android_toolchain)   then error('You must set the $ANDROID_TOOLCHAIN_ARM environment variable to the location of the android standalone toolchain')  end
    elseif arch.is('x86') then
        if (not android_toolchain)   then error('You must set the $ANDROID_TOOLCHAIN_X86 environment variable to the location of the android standalone toolchain')  end
    end

    if (not os.isdir(android_sdk))             then error('$ANDROID_SDK directory not found: ' .. android_sdk) end
    if (not os.isdir(android_ndk))             then error('$ANDROID_NDK directory not found: ' .. android_ndk) end
    if (not os.isdir(android_toolchain))             then error('$ANDROID_TOOLCHAIN directory not found: ' .. android_toolchain) end

    local ndk_toolchain_dir = nil
    local stl_lib_path      = nil
    if arch.is('arm') then
        ndk_toolchain_dir  = path.join(android_toolchain, 'arm-linux-androideabi')
        stl_lib_path       = path.join(android_toolchain, 'arm-linux-androideabi', 'lib')
        compilers.GCC_CC   = path.join(android_toolchain, 'bin', 'arm-linux-androideabi-gcc')
        compilers.GCC_CXX  = path.join(android_toolchain, 'bin', 'arm-linux-androideabi-g++')
        compilers.GCC_AR   = path.join(android_toolchain, 'bin', 'arm-linux-androideabi-ar')
    elseif arch.is('x86') then
        ndk_toolchain_dir  = path.join(android_toolchain, 'i686-linux-android')
        stl_lib_path       = path.join(android_toolchain, 'i686-linux-android', 'lib')
        compilers.GCC_CC   = path.join(android_toolchain, 'bin', 'i686-linux-android-gcc')
        compilers.GCC_CXX  = path.join(android_toolchain, 'bin', 'i686-linux-android-g++')
        compilers.GCC_AR   = path.join(android_toolchain, 'bin', 'i686-linux-android-ar')
    else
        error('unsupported archiecture  for android ndk builds')
    end

    local ndk_sysroot       = path.join(android_toolchain, 'sysroot')
    local stl_include_path  = path.join(android_toolchain, 'include', 'c++', '4.6')

    if (not os.isdir(ndk_toolchain_dir)) then error('NDK toolchain not found in ' .. ndk_toolchain_dir) end
    if (not os.isdir(ndk_sysroot))       then error('NDK sysroot not found in ' .. ndk_sysroot)         end

    compilers.GCC_CC_FLAGS      = "'--sysroot=" .. translate_path_for_tool(ndk_sysroot) .. "'"
    compilers.GCC_CXX_FLAGS     = compilers.GCC_CC_FLAGS .. " '-L" .. translate_path_for_tool(stl_lib_path) .. "' '-isystem" .. translate_path_for_tool(stl_include_path) .. "'"

    gccflags.Optimize = "-O"
    compilers.Mono_CSC_DEFINES = { "MONO" }
    compilers.MSNET_CSC_DEFINES = { "MONO" }
end

if platform.is('linux') then
    compilers.Mono_CSC_DEFINES = { "MONO" }
    compilers.MSNET_CSC_DEFINES = { "MONO" }
    defines { "PLATFORM_LINUX" }

    if arch.is('x86') then
        compilers.GCC_CC_FLAGS  = "-fPIC -D_FILE_OFFSET_BITS=64 -D_LARGEFILE_SOURCE -Wl,-rpath,\\$$ORIGIN"
        compilers.GCC_CXX_FLAGS = compilers.GCC_CC_FLAGS .. " -static-libstdc++ -static-libgcc"
        gccflags.Optimize       = "-O2 -fomit-frame-pointer -funroll-all-loops -finline-functions -ffast-math"

        local xtools                = os.getenv('X86_XTOOLS')
        local xtools_sysroot        = os.getenv('X86_XTOOLS_SYSROOT')
        local cross_compile         = os.getenv('X86_CROSS_COMPILE')
        if (xtools) then
            compilers.GCC_CC            = path.join(xtools, 'bin', cross_compile .. '-gcc')
            compilers.GCC_CXX           = path.join(xtools, 'bin', cross_compile .. '-g++')
            compilers.GCC_AR            = path.join(xtools, 'bin', cross_compile .. '-ar')
            compilers.GCC_CC_FLAGS      = compilers.GCC_CC_FLAGS .. " '--sysroot=" .. translate_path_for_tool(xtools_sysroot) .. "'"
        end
    elseif arch.is('x64') then
        compilers.GCC_CC_FLAGS  = "-fPIC -D_FILE_OFFSET_BITS=64 -D_LARGEFILE_SOURCE -Wl,-rpath,\\$$ORIGIN"
        compilers.GCC_CXX_FLAGS = compilers.GCC_CC_FLAGS .. " -static-libstdc++ -static-libgcc"
        gccflags.Optimize       = "-O2 -fomit-frame-pointer -funroll-all-loops -finline-functions -ffast-math"

        local xtools                = os.getenv('X64_XTOOLS')
        local xtools_sysroot        = os.getenv('X64_XTOOLS_SYSROOT')
        local cross_compile         = os.getenv('X64_CROSS_COMPILE')
        if (xtools) then
            compilers.GCC_CC            = path.join(xtools, 'bin', cross_compile .. '-gcc')
            compilers.GCC_CXX           = path.join(xtools, 'bin', cross_compile .. '-g++')
            compilers.GCC_AR            = path.join(xtools, 'bin', cross_compile .. '-ar')
            compilers.GCC_CC_FLAGS      = compilers.GCC_CC_FLAGS .. " '--sysroot=" .. translate_path_for_tool(xtools_sysroot) .. "'"
        end

    elseif arch.is('arm') then
        local xtools                = os.getenv('ARM_XTOOLS')
        local xtools_sysroot        = os.getenv('ARM_XTOOLS_SYSROOT')
        local cross_compile         = os.getenv('ARM_CROSS_COMPILE')

        if (not cross_compile) then cross_compile = "arm-unknown-linux-gnueabi-" end

        if (not xtools)         then error('You must set the ARM_XTOOLS environment variable to the location of the cross-compilation toolchain')         end
        if (not xtools_sysroot) then error('You must set the ARM_XTOOLS_SYSROOT environment variable to the location of the cross-compilation toolchain') end

        compilers.GCC_CC            = path.join(xtools, 'bin', cross_compile .. '-gcc')
        compilers.GCC_CXX           = path.join(xtools, 'bin', cross_compile .. '-g++')
        compilers.GCC_AR            = path.join(xtools, 'bin', cross_compile .. '-ar')
        compilers.GCC_CC_FLAGS      = "-ggdb3 -fPIC -marm -march=armv7-a -mfloat-abi=hard -mfpu=neon -DARM_FPU_VFP_HARD -mthumb-interwork -mtune=cortex-a9 '--sysroot=" .. translate_path_for_tool(xtools_sysroot) .. "' -D_FILE_OFFSET_BITS=64 -D_LARGEFILE_SOURCE"
        if os.getenv("SONAVOX") ~= nil then
            compilers.GCC_CXX_FLAGS     = compilers.GCC_CC_FLAGS
        else
            compilers.GCC_CXX_FLAGS     = compilers.GCC_CC_FLAGS  .. " -static-libstdc++ -static-libgcc"
        end
        gccflags.Optimize           = "-O2"
    end
end

if system.is('windows') then
   defines {"SYSTEM_WINDOWS"}
elseif system.is('linux') then
   defines {"SYSTEM_LINUX"}
elseif system.is('macosx') then
   defines {"SYSTEM_MACOSX", "SYSTEM_OSX"}
elseif system.is('ios') then
   defines {"SYSTEM_IOS", "SYSTEM_IOS"}
end

-----------------------------------------------------------------------------------------
-----------------------------------------------------------------------------------------
--
-- Utilities for dealing with Binaries/
--
-----------------------------------------------------------------------------------------
-----------------------------------------------------------------------------------------

function copybinaries_managed(o)
    if (type(o) == "table") then
        for _,i in ipairs(o) do
            copybinary_managed(i)
        end
    else
        copybinary_managed(o)
    end
end

function copybinary_managed(o)
    copyfiles { path.getrelative(genmake.current.subdir, path.join("Binaries", "managed", o)) }
end

function copybinaries_sharedlib(o)
    if (type(o) == "table") then
        for _,i in ipairs(o) do
            copybinary_sharedlib(i)
        end
    else
        copybinary_sharedlib(o)
    end
end

function copybinary_sharedlib(o)
    local prefix = ''
    local suffix = ''

    if system.is('windows') then
        suffix = '.dll'
    elseif system.is('linux') then
        prefix = 'lib'
        suffix = '.so'
    elseif system.is('ios') then
        prefix = 'lib'
        suffix = '.a'
    elseif system.is('macosx') then
        prefix = 'lib'
        suffix = '.dylib'
    end

    copyfiles { path.getrelative(genmake.current.subdir, path.join("Binaries", platform.get(), arch.get(), prefix .. o .. suffix)) }
end

function copybinaries_exe(o)
    if (type(o) == "table") then
        for _,i in ipairs(o) do
            copybinary_exe(i)
        end
    else
        copybinary_exe(o)
    end
end

function copybinary_exe(o)
    local suffix = ''

    if system.is('windows') then
        suffix = '.exe'
    end

    copyfiles { path.getrelative(genmake.current.subdir, path.join("Binaries", platform.get(), arch.get(), o .. suffix)) }
end

function linkbinaries_staticlib(o)
    if (type(o) == "table") then
        for _,i in ipairs(o) do
            linkbinary_staticlib(i)
        end
    else
        linkbinary_staticlib(o)
    end
end

function linkbinary_staticlib(o)
    linkfiles { path.getrelative(genmake.current.subdir, path.join("Binaries", platform.get(), arch.get(), o)) }
end

function linkbinaries_sharedlib(o)
    if (type(o) == "table") then
        for _,i in ipairs(o) do
            linkbinary_sharedlib(i)
        end
    else
        linkbinary_sharedlib(o)
    end
end

function linkbinary_sharedlib(o)
    if system.is('windows') then
        linkbinary_staticlib(o)
        copybinary_sharedlib(o)
    else
        local prefix = ''
        local suffix = ''
        if system.is('linux') then
            prefix = 'lib'
            suffix = '.so'
        elseif system.is('macosx') then
            prefix = 'lib'
            suffix = '.dylib'
        end
        linkfiles { path.getrelative(genmake.current.subdir, path.join("Binaries", platform.get(), arch.get(), prefix .. o .. suffix)) }
    end
end

function linkbinaries_managed(o)
    if (type(o) == "table") then
        for _,i in ipairs(o) do
            linkbinary_managed(i)
        end
    else
        linkbinary_managed(o)
    end
end

function linkbinary_managed(o)
    linkfiles { path.getrelative(genmake.current.subdir, path.join("Binaries", "managed", o)) }
end

function linkbinary_managed_platform(o)
    linkfiles { path.getrelative(genmake.current.subdir, path.join("Binaries", platform.get(), "managed", o)) }
end
