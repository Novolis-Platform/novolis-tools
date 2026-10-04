using Novolis.Tools.CanvasHtml;
using TUnit.Core;

namespace Novolis.Tools.CanvasHtml.Unit;

public sealed class CanvasHtmlTests
{
    [Test]
    public async Task Render_WritesHeadingTextAndStripsTypes()
    {
        const string source = """
            import { H1, Stack, Text } from "cursor/canvas";

            type Zone = { name: string; meters: number };
            const zones: Zone[] = [{ name: "Hold", meters: 29 }];

            export default function Demo() {
              const rev = "B";
              return (
                <Stack gap={8}>
                  <H1>Calypso Rev {rev}</H1>
                  <Text>{zones[0].name}</Text>
                </Stack>
              );
            }
            """;

        var html = CanvasHtmlRenderer.Render(source, "Demo");

        await Assert.That(html).Contains("<h1");
        await Assert.That(html).Contains("Calypso Rev B");
        await Assert.That(html).Contains("Hold");
        await Assert.That(html).Contains("<style>");
        await Assert.That(html).DoesNotContain("cursor/canvas");
    }

    [Test]
    public async Task Render_EvaluatesCastsArrowsAndSvg()
    {
        const string source = """
            import { Stack } from "cursor/canvas";

            const pts: [number, number][] = [[1, 2] as [number, number]];
            const label = "orange" as const;
            const scale = (m: number) => m * 2;

            export default function Demo() {
              return (
                <Stack>
                  <svg width="40" height="20" viewBox="0 0 40 20">
                    <text x={scale(pts[0][0])} y={10}>{label}</text>
                  </svg>
                </Stack>
              );
            }
            """;

        var html = CanvasHtmlRenderer.Render(source, "Demo");

        await Assert.That(html).Contains("<svg");
        await Assert.That(html).Contains("orange");
        await Assert.That(html).Contains("x=\"2\"");
    }

    [Test]
    public async Task Transpile_KeepsObjectReturnTypeOutOfTheBody()
    {
        var javascript = new TsxTranspiler().Transpile("""
            function tilePlates(lenAlong: number, lenAcross: number): { count: number; orient: string } {
              const along6 = lenAlong + lenAcross;
              return { count: along6, orient: "x" };
            }
            export default function Demo() { return null; }
            """).JavaScript;

        await Assert.That(javascript).Contains("function tilePlates(lenAlong, lenAcross)");
        await Assert.That(javascript).Contains("const along6 = lenAlong + lenAcross");
        await Assert.That(javascript).DoesNotContain("orient: string");
    }

    [Test]
    public async Task Render_JsxInsideAttribute()
    {
        const string source = """
            import { Card, CardHeader, Pill } from "cursor/canvas";
            export default function Demo() {
              return (
                <Card>
                  <CardHeader trailing={<Pill tone="success" size="sm">Yes</Pill>}>
                    Novolis Blueprints
                  </CardHeader>
                </Card>
              );
            }
            """;

        var html = CanvasHtmlRenderer.Render(source, "Demo");
        await Assert.That(html).Contains("Novolis Blueprints");
        await Assert.That(html).Contains("Yes");
        await Assert.That(html).DoesNotContain("<Pill");
    }

    [Test]
    public async Task Combine_PutsEachCanvasOnItsOwnTab()
    {
        var html = CanvasHtmlRenderer.Combine(
        [
            new CanvasPage("Hull", "<h1>Hull sheet</h1>"),
            new CanvasPage("Cabin", "<h1>Cabin sheet</h1>"),
        ],
        "Calypso");

        await Assert.That(html).Contains("<title>Calypso</title>");
        await Assert.That(html).Contains("Hull sheet");
        await Assert.That(html).Contains("Cabin sheet");
        await Assert.That(html).Contains("id=\"cv-tab-0\" checked");
        await Assert.That(html).Contains("id=\"cv-tab-1\"");
        await Assert.That(html).Contains("#cv-panel-0");
        await Assert.That(html).Contains("#cv-panel-1");
        await Assert.That(html).Contains("display: none");
    }
}
