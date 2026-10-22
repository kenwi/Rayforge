# Third-party notices

Rayforge is MIT-licensed (see `LICENSE`). The following third-party components are used by, or distributed
with, Rayforge packages and their dependencies.

| Component | Version / source | License |
|---|---|---|
| [Raylib](https://github.com/raysan5/raylib) | native raylib 6.0, supplied by Raylib-cs | Zlib |
| [Raylib-cs](https://github.com/raylib-cs/raylib-cs) | 8.1.0, managed bindings + native assets | Zlib |
| [.NET / ASP.NET Core](https://github.com/dotnet/aspnetcore) | 10.0.x | MIT |
| [Blazor WebAssembly](https://github.com/dotnet/runtime) | 10.0.x | MIT |

Rayforge does not vendor a raylib binary. The `Raylib-cs` package supplies the native archive for
`browser-wasm` and the desktop runtimes; its own notices and license apply to those files.

## Zlib license (Raylib, Raylib-cs)

Copyright (c) 2013-2026 Ramon Santamaria (@raysan5)
Copyright (C) 2018-2025 raylib-cs

Upstream license files:

- https://github.com/raysan5/raylib/blob/master/LICENSE
- https://github.com/raylib-cs/raylib-cs/blob/master/LICENSE

This software is provided 'as-is', without any express or implied warranty. In no event will the authors be
held liable for any damages arising from the use of this software.

Permission is granted to anyone to use this software for any purpose, including commercial applications, and
to alter it and redistribute it freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not claim that you wrote the original
   software. If you use this software in a product, an acknowledgment in the product documentation would be
   appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be misrepresented as being the
   original software.
3. This notice may not be removed or altered from any source distribution.

## MIT license (.NET, ASP.NET Core, Blazor)

Copyright (c) .NET Foundation and Contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
documentation files (the "Software"), to deal in the Software without restriction, including without
limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the
Software, and to permit persons to whom the Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions
of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.
