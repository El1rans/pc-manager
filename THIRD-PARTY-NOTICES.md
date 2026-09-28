# Third-party notices

## OpenRGB.NET

Porchlight's Lighting feature vendors a copy of [OpenRGB.NET](https://github.com/diogotr7/OpenRGB.NET)
3.1.1 at `src/ThirdParty/OpenRGB.NET/`, instead of referencing it as a NuGet package, because the
released version has a bug with no released fix at time of writing: it does not detect a graceful
remote close (OpenRGB being closed, or its SDK server being stopped) - see
`docs/upstream/openrgb-net.md` for the root cause and the two small, targeted patches applied,
each marked with a `// Porchlight patch:` comment in the vendored source.

OpenRGB.NET is used under the MIT License:

```
MIT License

Copyright (c) 2022 Diogo Trindade

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

The full, unmodified license text is also kept at `src/ThirdParty/OpenRGB.NET/LICENSE`.
