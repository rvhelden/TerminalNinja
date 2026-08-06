using TerminalNinja.Controls.Charts;

namespace TerminalNinja.Tests.Unit.Controls.Charts;

/// <summary>Tests for the <see cref="GraphNode"/> and <see cref="GraphEdge"/> data models.</summary>
public class GraphModelTests
{
    [Test]
    public async Task GraphNode_Defaults_AreEmpty()
    {
        var node = new GraphNode();

        await Assert.That(node.Id).IsEqualTo("");
        await Assert.That(node.Name).IsEqualTo("");
        await Assert.That(node.Value).IsEqualTo(0d);
        await Assert.That(node.Color).IsEqualTo(Color.Transparent);
    }

    [Test]
    public async Task GraphEdge_Defaults_AreEmpty()
    {
        var edge = new GraphEdge();

        await Assert.That(edge.From).IsEqualTo("");
        await Assert.That(edge.To).IsEqualTo("");
        await Assert.That(edge.Color).IsEqualTo(Color.Transparent);
    }

    // TerminalXaml.Load<T> requires a FrameworkElement root, so the POCOs are parsed
    // as hosted content — the same way they appear inside a NodeGraph in real markup.

    [Test]
    public async Task Xaml_GraphNode_ParsesAttributes()
    {
        const string xaml = """
            <ContentControl xmlns="http://schemas.terminalninja.dev/xaml">
                <GraphNode Id="web" Name="Web Server" Value="3" Color="#FF0000" />
            </ContentControl>
            """;

        var host = TerminalXaml.Load<ContentControl>(xaml);
        var node = (GraphNode)host.Content!;

        await Assert.That(node.Id).IsEqualTo("web");
        await Assert.That(node.Name).IsEqualTo("Web Server");
        await Assert.That(node.Value).IsEqualTo(3d);
        await Assert.That(node.Color).IsEqualTo(new Color(255, 0, 0));
    }

    [Test]
    public async Task Xaml_GraphNode_TextContentSetsName()
    {
        const string xaml = """
            <ContentControl xmlns="http://schemas.terminalninja.dev/xaml">
                <GraphNode Id="db">Database</GraphNode>
            </ContentControl>
            """;

        var host = TerminalXaml.Load<ContentControl>(xaml);
        var node = (GraphNode)host.Content!;

        await Assert.That(node.Name).IsEqualTo("Database");
    }

    [Test]
    public async Task Xaml_GraphEdge_ParsesAttributes()
    {
        const string xaml = """
            <ContentControl xmlns="http://schemas.terminalninja.dev/xaml">
                <GraphEdge From="web" To="db" Color="0,255,0" />
            </ContentControl>
            """;

        var host = TerminalXaml.Load<ContentControl>(xaml);
        var edge = (GraphEdge)host.Content!;

        await Assert.That(edge.From).IsEqualTo("web");
        await Assert.That(edge.To).IsEqualTo("db");
        await Assert.That(edge.Color).IsEqualTo(new Color(0, 255, 0));
    }
}
