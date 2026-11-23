using System.Runtime.CompilerServices;

// Core のテスト (Unity の EditMode と dotnet の両方で同じ名前) が、ここへ移す internal の読み書きを公開の API にせずに直接確かめるため。ソースのないアセンブリを作らないためでもある
[assembly: InternalsVisibleTo("Shooter.Core.Tests")]
