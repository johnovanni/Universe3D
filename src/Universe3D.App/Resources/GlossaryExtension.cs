using System.Windows.Markup;
using Universe3D.Core.Text;

namespace Universe3D.App.Resources;

/// <summary>
/// <c>ToolTip="{res:Glossary Redshift}"</c>: a tooltip text from <see cref="Glossary"/>. Several keys separated by
/// spaces give their texts as separate paragraphs. An unknown key fails when the window loads.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class GlossaryExtension : MarkupExtension
{
    public GlossaryExtension() { }

    public GlossaryExtension(string keys) => Keys = keys;

    [ConstructorArgument("keys")]
    public string Keys { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Text(Keys);

    public static string Text(string keys) =>
        string.Join(Environment.NewLine + Environment.NewLine,
            keys.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Glossary.Get));
}
