# Third-party notices

Installer is licensed under the MIT License (see `LICENSE`). The release zip also contains the third-party components
below, each under its own license. Both files, and `SkiaSharp-THIRD-PARTY-NOTICES.txt`, ship in every release zip.

## .NET libraries (MIT)

- Microsoft.Extensions.Configuration, .Configuration.Abstractions, .Configuration.Binder,
  .DependencyInjection, .DependencyInjection.Abstractions, .Logging, .Logging.Abstractions,
  .Logging.Configuration, .Logging.Console, .Options, .Options.ConfigurationExtensions, .Primitives 10.0.12
  (https://github.com/dotnet/runtime)
- System.CommandLine 2.0.12 (https://github.com/dotnet/command-line-api)

```
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

The .NET runtime's own third-party notices: https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT

## SkiaSharp 3.119.4 (MIT)

`SkiaSharp.dll` and the native `libSkiaSharp` libraries in `runtimes/osx`, `runtimes/linux-x64` and
`runtimes/linux-arm64` (https://github.com/mono/SkiaSharp).

```
Copyright (c) 2015-2016 Xamarin, Inc.
Copyright (c) 2017-2018 Microsoft Corporation.

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

The native libraries include Skia (BSD-3-Clause), FreeType 2.13.3 (FreeType License; Linux only), libpng, zlib,
expat (macOS), libjpeg-turbo, libwebp, the Adobe DNG SDK, piex, wuffs and Vulkan Memory Allocator. Their full
copyright notices and license texts are in `SkiaSharp-THIRD-PARTY-NOTICES.txt`, copied unchanged from the SkiaSharp
3.119.4 NuGet package.

Portions of this software are copyright © 2024 The FreeType Project (www.freetype.org). All rights reserved.

This product includes DNG technology under license by Adobe Systems Incorporated.

## Repository test data (not in the release zip)

The sample binaries in `solution/UnitTests.Installer.Cli/TestData/generic/` were built with Go 1.24.1 and contain the
Go runtime and standard library (BSD-3-Clause):

```
Copyright 2009 The Go Authors.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

   * Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above
copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the
distribution.
   * Neither the name of Google LLC nor the names of its
contributors may be used to endorse or promote products derived from
this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

The files in `solution/UnitTests.Installer.Services/TestData/Headers/` are the first 4 KB of .NET apphost executables
produced by `dotnet publish` (MIT, .NET Foundation and Contributors; license above).

## Tools the builder runs but does not include

- NSIS (`makensis`): zlib/libpng license, https://nsis.sourceforge.io/License. Setup executables it produces carry the
  NSIS stub; that license asks nothing of the installers' authors.
- appimagetool 1.9.1 (MIT) and the Debian base image, downloaded into a locally built Docker image.
- The AppImage type-2 runtime 20251108 (MIT), which is embedded in every AppImage the builder produces. It statically
  links musl, libfuse (LGPL-2.1), squashfuse, zstd and zlib; see
  https://github.com/AppImage/type2-runtime/blob/main/LICENSE. If you distribute AppImages, those terms apply to the
  runtime part of your AppImage (not to your app).
