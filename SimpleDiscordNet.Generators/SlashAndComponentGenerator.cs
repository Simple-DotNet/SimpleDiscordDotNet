using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SimpleDiscordNet.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class SlashAndComponentGenerator : IIncrementalGenerator
{
    private const string SlashAttr = "SimpleDiscordNet.Commands.SlashCommandAttribute";
    private const string SlashGroupAttr = "SimpleDiscordNet.Commands.SlashCommandGroupAttribute";
    private const string SlashSubGroupAttr = "SimpleDiscordNet.Commands.SlashCommandSubGroupAttribute";
    private const string ComponentAttr = "SimpleDiscordNet.Commands.ComponentHandlerAttribute";
    private const string ButtonHandlerAttr = "SimpleDiscordNet.Commands.ButtonHandlerAttribute";
    private const string SelectMenuHandlerAttr = "SimpleDiscordNet.Commands.SelectMenuHandlerAttribute";
    private const string UserSelectHandlerAttr = "SimpleDiscordNet.Commands.UserSelectHandlerAttribute";
    private const string RoleSelectHandlerAttr = "SimpleDiscordNet.Commands.RoleSelectHandlerAttribute";
    private const string ChannelSelectHandlerAttr = "SimpleDiscordNet.Commands.ChannelSelectHandlerAttribute";
    private const string MentionableSelectHandlerAttr = "SimpleDiscordNet.Commands.MentionableSelectHandlerAttribute";
    private const string NoDeferAttr = "SimpleDiscordNet.Commands.NoDeferAttribute";
    private const string EphemeralAttr = "SimpleDiscordNet.Commands.EphemeralAttribute";
    private const string RequirePermissionsAttr = "SimpleDiscordNet.Commands.RequirePermissionsAttribute";
    private const string CommandOptionAttr = "SimpleDiscordNet.Commands.CommandOptionAttribute";
    private const string CommandChoiceAttr = "SimpleDiscordNet.Commands.CommandChoiceAttribute";
    private const string UserContextMenuAttr = "SimpleDiscordNet.Commands.UserContextMenuAttribute";
    private const string MessageContextMenuAttr = "SimpleDiscordNet.Commands.MessageContextMenuAttribute";
    private const string AutocompleteAttr = "SimpleDiscordNet.Commands.AutocompleteAttribute";
    private const string GuildOnlyAttr = "SimpleDiscordNet.Commands.GuildOnlyAttribute";
    private const string AllowInDmsAttr = "SimpleDiscordNet.Commands.AllowInDMsAttribute";
    private const string UserInstallableAttr = "SimpleDiscordNet.Commands.UserInstallableAttribute";
    private const string LocalizedAttr = "SimpleDiscordNet.Commands.LocalizedAttribute";
    private const string ModalAttr = "SimpleDiscordNet.Commands.ModalAttribute";
    private const string ModalHandlerAttr = "SimpleDiscordNet.Commands.ModalHandlerAttribute";
    private const string ModalAttrPrefix = "SimpleDiscordNet.Commands.Modal";
    private const string OnEventAttr = "SimpleDiscordNet.Commands.OnEventAttribute";
    private const string OnCommandErrorAttr = "SimpleDiscordNet.Commands.OnCommandErrorAttribute";
    private const string GenerateHelpAttr = "SimpleDiscordNet.Commands.GenerateHelpAttribute";
    private const string CooldownAttr = "SimpleDiscordNet.Commands.CooldownAttribute";
    private const string RequireRoleAttr = "SimpleDiscordNet.Commands.RequireRoleAttribute";
    private const string RequireBotPermissionsAttr = "SimpleDiscordNet.Commands.RequireBotPermissionsAttribute";
    private const string RequireOwnerAttr = "SimpleDiscordNet.Commands.RequireOwnerAttribute";
    private const string DiscordEventsType = "SimpleDiscordNet.Events.DiscordEvents";
    private const string Checks = "global::SimpleDiscordNet.Commands.CommandChecks";
    private const string InteractionContextType = "SimpleDiscordNet.Commands.InteractionContext";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var methodsWithAttrs = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0,
                                  static (ctx, ct) => GetCandidate(ctx))
            .Where(static c => c is not null)!
            .Select(static (c, _) => c!);

        var modalForms = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => node is TypeDeclarationSyntax t && t.AttributeLists.Count > 0,
                                  static (ctx, ct) => GetModalForm(ctx))
            .Where(static f => f is not null)!
            .Select(static (f, _) => f!);

        var compilationAndCandidates = context.CompilationProvider
            .Combine(methodsWithAttrs.Collect())
            .Combine(modalForms.Collect());

        context.RegisterSourceOutput(compilationAndCandidates, static (spc, tuple) =>
        {
            var ((compilation, items), forms) = tuple;
            var help = compilation.Assembly.GetAttributes().FirstOrDefault(a => AttributeName(a) == GenerateHelpAttr);
            Emit(spc, compilation.AssemblyName ?? "Assembly", items, forms, help);
        });
    }

    private static string? AttributeName(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':');

    /// <summary>
    /// Reads check attributes as generated code. Method attributes win over class attributes of the same kind.
    /// Cooldowns go last, so a user who fails another check doesn't use up their cooldown.
    /// </summary>
    private static List<string> ReadChecks(IMethodSymbol method)
    {
        var found = new Dictionary<string, string>();
        var cooldownKey = method.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + "." + method.Name;

        void Read(IEnumerable<AttributeData> attributes)
        {
            foreach (var attribute in attributes)
            {
                var name = AttributeName(attribute);
                if (name is null || found.ContainsKey(name)) continue;

                string message = NamedString(attribute, "Message");
                if (name == RequireOwnerAttr)
                {
                    found[name] = $"{Checks}.RequireOwner({message})";
                }
                else if (name == RequireRoleAttr && attribute.ConstructorArguments.Length >= 1)
                {
                    var roles = ArrayArgument(attribute.ConstructorArguments[0]).Select(Lit);
                    found[name] = $"{Checks}.RequireRole(new string[] {{ {string.Join(", ", roles)} }}, {message})";
                }
                else if (name == RequireBotPermissionsAttr && attribute.ConstructorArguments.Length >= 1)
                {
                    ulong permissions = Convert.ToUInt64(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture);
                    found[name] = $"{Checks}.RequireBotPermissions({permissions.ToString(CultureInfo.InvariantCulture)}UL, {message})";
                }
                else if (name == CooldownAttr && attribute.ConstructorArguments.Length >= 1)
                {
                    double seconds = Convert.ToDouble(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture);
                    object? scope = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Scope").Value.Value;
                    string scopeValue = Convert.ToInt32(scope ?? 0, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                    found[name] = $"{Checks}.Cooldown({Lit(cooldownKey)}, {seconds.ToString("R", CultureInfo.InvariantCulture)}, (global::SimpleDiscordNet.Commands.CooldownScope){scopeValue}, {message})";
                }
            }
        }

        Read(method.GetAttributes());
        Read(method.ContainingType.GetAttributes());

        return found.OrderBy(static c => c.Key == CooldownAttr ? 1 : 0).Select(static c => c.Value).ToList();
    }

    private static string ChecksField(Candidate handler)
        => handler.Checks.Count == 0
            ? string.Empty
            : $", Checks: new global::SimpleDiscordNet.Commands.CommandCheck[] {{ {string.Join(", ", handler.Checks)} }}";

    private static ITypeSymbol WithoutNullable(ITypeSymbol type)
        => type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable ? nullable.TypeArguments[0] : type;

    private static List<string> EnumMembers(ITypeSymbol type)
        => type.GetMembers().OfType<IFieldSymbol>().Where(static f => f.HasConstantValue).Select(static f => f.Name).ToList();

    /// <summary>
    /// Turns a name like DarkRed into "Dark Red" for showing to users.
    /// </summary>
    private static string SpacedName(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) sb.Append(' ');
            sb.Append(name[i]);
        }
        return sb.ToString();
    }

    private static string EnumFromString(string source, ITypeSymbol enumType, string fallback, bool nullable)
    {
        string type = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string cast = nullable ? $"({type}?)" : string.Empty;
        var arms = EnumMembers(enumType).Select(m => $"{Lit(m)} => {cast}{type}.{m}");
        return $"({source} switch {{ {string.Join(", ", arms)}, _ => {fallback} }})";
    }

    /// <summary>
    /// Reads a handler's return type, so generated code can await it and send it as the reply.
    /// </summary>
    private static (ReturnShape Shape, string? Reply) ReadReturn(IMethodSymbol method)
    {
        if (method.ReturnsVoid) return (ReturnShape.Void, null);

        ITypeSymbol? value = method.ReturnType;
        bool awaitable = false;
        if (method.ReturnType is INamedTypeSymbol named && named.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks" && named.Name is "Task" or "ValueTask")
        {
            awaitable = true;
            value = named.TypeArguments.Length == 1 ? named.TypeArguments[0] : null;
        }

        if (value is null) return (ReturnShape.Awaitable, null);

        string? reply = value.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimEnd('?') switch
        {
            "string" => "string",
            "global::SimpleDiscordNet.Primitives.EmbedBuilder" or "global::SimpleDiscordNet.EmbedBuilder" => "embed",
            "global::SimpleDiscordNet.MessageBuilder" => "message",
            _ => null
        };

        return (awaitable ? ReturnShape.AwaitableValue : ReturnShape.Value, reply);
    }

    private static string ReplyCode(string? reply)
    {
        return reply switch
        {
            "embed" => "if (_res is not null) await ctx.RespondAsync(embed: _res, ct: ct).ConfigureAwait(false);",
            null => string.Empty,
            _ => "if (_res is not null) await ctx.RespondAsync(_res, ct: ct).ConfigureAwait(false);"
        };
    }

    /// <summary>
    /// Writes the code that calls a handler and awaits it. With replies allowed, a returned string, embed
    /// or message is sent as the response.
    /// </summary>
    private static string CallCode(Candidate handler, string call, bool allowReply = true)
    {
        const string completed = "await global::System.Threading.Tasks.Task.CompletedTask;";
        string? reply = allowReply ? handler.ReplyKind : null;

        return handler.ReturnKind switch
        {
            ReturnShape.Awaitable => $"await {call}.ConfigureAwait(false);",
            ReturnShape.AwaitableValue when reply is null => $"_ = await {call}.ConfigureAwait(false);",
            ReturnShape.AwaitableValue => $"var _res = await {call}.ConfigureAwait(false); {ReplyCode(reply)}",
            ReturnShape.Value when reply is null => $"_ = {call}; {completed}",
            ReturnShape.Value => $"var _res = {call}; {ReplyCode(reply)}",
            _ => $"{call}; {completed}"
        };
    }

    private static string? DefaultCode(IParameterSymbol parameter, ITypeSymbol core)
    {
        if (!parameter.HasExplicitDefaultValue || parameter.ExplicitDefaultValue is null) return null;

        string literal = SymbolDisplay.FormatPrimitive(parameter.ExplicitDefaultValue, quoteStrings: true, useHexadecimalNumbers: false);
        return core.TypeKind == TypeKind.Enum
            ? $"({core.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})({literal})"
            : literal;
    }

    /// <summary>
    /// Writes the code that converts a named part of a component ID, or null for an unsupported type.
    /// </summary>
    private static string? TemplateValueCode(string name, ITypeSymbol type)
    {
        const string culture = "global::System.Globalization.CultureInfo.InvariantCulture";
        string value = $"ctx.GetIdValue({Lit(name)})";

        if (type.TypeKind == TypeKind.Enum)
        {
            return EnumFromString(value, type, $"default({type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})", nullable: false);
        }

        return type.SpecialType switch
        {
            SpecialType.System_String => $"{value} ?? string.Empty",
            SpecialType.System_Int32 => $"int.Parse({value}!, {culture})",
            SpecialType.System_Int64 => $"long.Parse({value}!, {culture})",
            SpecialType.System_UInt64 => $"ulong.Parse({value}!, {culture})",
            SpecialType.System_Boolean => $"bool.Parse({value}!)",
            _ => null
        };
    }

    private static List<string> TemplateNames(string template)
    {
        var names = new List<string>();
        int position = 0;
        while (true)
        {
            int open = template.IndexOf('{', position);
            int close = open < 0 ? -1 : template.IndexOf('}', open);
            if (close < 0) return names;
            names.Add(template.Substring(open + 1, close - open - 1));
            position = close + 1;
        }
    }

    /// <summary>
    /// Returns the event an attribute listens for. [OnEvent] names it, and shortcuts like [OnMemberJoined] are named after it.
    /// </summary>
    private static string? ReadEventName(AttributeData attribute)
    {
        if (attribute.AttributeClass is not { } type) return null;

        if (AttributeName(attribute) == OnEventAttr)
            return attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as string : null;

        if (type.BaseType?.ToDisplayString() != OnEventAttr) return null;

        // The shortcut's name is the event's name, except where the event has a longer one
        string name = type.Name.Substring(2, type.Name.Length - 2 - "Attribute".Length);
        return name == "DirectMessage" ? "DirectMessageReceived" : name;
    }

    private static LocalizedText? ReadLocalized(AttributeData attribute)
    {
        if (AttributeName(attribute) != LocalizedAttr || attribute.ConstructorArguments.Length < 2) return null;
        return new LocalizedText
        {
            Locale = attribute.ConstructorArguments[0].Value as string ?? string.Empty,
            Name = attribute.ConstructorArguments[1].Value as string ?? string.Empty,
            Description = attribute.ConstructorArguments.Length >= 3 ? attribute.ConstructorArguments[2].Value as string : null
        };
    }

    /// <summary>
    /// Writes a value as a C# string literal, or null.
    /// </summary>
    private static string Lit(string? value)
    {
        if (value is null) return "null";
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    }

    private static string OptionalInt(int value) => value > 0 ? value.ToString(CultureInfo.InvariantCulture) : "null";

    private static string NamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value is string value ? Lit(value) : "null";

    private static int NamedInt(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value is int value ? value : 0;

    private static bool? NamedBool(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value is bool value ? value : null;

    private static string[] ArrayArgument(TypedConstant constant)
        => constant.Kind == TypedConstantKind.Array
            ? constant.Values.Select(static v => v.Value as string ?? string.Empty).ToArray()
            : [];

    private static (string Label, string Value) SplitOption(string option)
    {
        int separator = option.IndexOf(':');
        return separator > 0 ? (option.Substring(0, separator), option.Substring(separator + 1)) : (option, option);
    }

    private static ModalFormCandidate? GetModalForm(GeneratorSyntaxContext ctx)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not INamedTypeSymbol type) return null;

        var modal = type.GetAttributes().FirstOrDefault(a => AttributeName(a) == ModalAttr);
        if (modal is null) return null;

        var form = new ModalFormCandidate
        {
            TypeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            DisplayName = type.Name,
            Location = type.Locations.FirstOrDefault() ?? Location.None,
            Id = (modal.NamedArguments.FirstOrDefault(a => a.Key == "Id").Value.Value as string) ?? type.Name,
            Title = modal.ConstructorArguments.Length >= 1 ? modal.ConstructorArguments[0].Value as string ?? type.Name : type.Name,
            HasDefaultCtor = type.InstanceConstructors.Any(static c => c.DeclaredAccessibility != Accessibility.Private && c.Parameters.Length == 0)
        };

        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            var fieldAttr = property.GetAttributes().FirstOrDefault(a => AttributeName(a)?.StartsWith(ModalAttrPrefix, StringComparison.Ordinal) == true);
            if (fieldAttr is null) continue;

            ModalField? field = ReadModalField(property, fieldAttr);
            if (field is null || property.SetMethod is null)
                form.Problems.Add($"{property.Name} ({property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)})");
            else
                form.Fields.Add(field);
        }

        return form;
    }

    private static ModalField? ReadModalField(IPropertySymbol property, AttributeData attribute)
    {
        string kind = AttributeName(attribute)!.Substring(ModalAttrPrefix.Length).Replace("Attribute", string.Empty);
        string id = Lit(property.Name);
        string typeName = property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat).TrimEnd('?');
        bool isNullable = property.NullableAnnotation == NullableAnnotation.Annotated || property.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        bool isArray = typeName.EndsWith("[]", StringComparison.Ordinal);
        string elementType = isArray ? typeName.Substring(0, typeName.Length - 2) : typeName;
        const string prims = "global::SimpleDiscordNet.Primitives";

        string label = attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as string ?? property.Name : property.Name;
        bool required = !isNullable;
        string[] options = attribute.ConstructorArguments.Length >= 2 ? ArrayArgument(attribute.ConstructorArguments[1]) : [];
        string selectOptions = string.Join(", ", options.Select(SplitOption).Select(o => $"new {prims}.SelectOption({Lit(o.Label)}, {Lit(o.Value)})"));
        string choiceOptions = string.Join(", ", options.Select(SplitOption).Select(o => $"new {prims}.ChoiceOption({Lit(o.Label)}, {Lit(o.Value)})"));
        string requiredText = required ? "true" : "false";
        string placeholder = NamedString(attribute, "Placeholder");

        string component;
        string bind;
        switch (kind)
        {
            case "Text" when typeName == "string":
                required = NamedBool(attribute, "Required") ?? required;
                component = $"new {prims}.TextInput({id}, null, {(NamedBool(attribute, "Paragraph") == true ? "2" : "1")}, {OptionalInt(NamedInt(attribute, "MinLength"))}, {OptionalInt(NamedInt(attribute, "MaxLength"))}, {(required ? "true" : "false")}, initial?.{property.Name}, {placeholder})";
                bind = isNullable ? $"ctx.GetModalValue({id})" : $"ctx.GetModalValue({id}) ?? string.Empty";
                break;

            case "Select" when elementType == "string" && options.Length > 0:
                component = isArray
                    ? $"new {prims}.StringSelect({id}, new {prims}.SelectOption[] {{ {selectOptions} }}, {placeholder}, {(required ? 1 : 0)}, {options.Length}) {{ required = {requiredText} }}"
                    : $"new {prims}.StringSelect({id}, new {prims}.SelectOption[] {{ {selectOptions} }}, {placeholder}) {{ required = {requiredText} }}";
                bind = isArray ? $"ctx.GetModalValues({id})" : isNullable ? $"ctx.GetModalValue({id})" : $"ctx.GetModalValue({id}) ?? string.Empty";
                break;

            case "Radio" when typeName == "string" && options.Length > 0:
                component = $"new {prims}.RadioGroup({id}, new {prims}.ChoiceOption[] {{ {choiceOptions} }}, {requiredText})";
                bind = isNullable ? $"ctx.GetModalValue({id})" : $"ctx.GetModalValue({id}) ?? string.Empty";
                break;

            case "Checkboxes" when typeName == "string[]" && options.Length > 0:
                component = $"new {prims}.CheckboxGroup({id}, new {prims}.ChoiceOption[] {{ {choiceOptions} }}, {requiredText}, {(required ? "null" : "0")})";
                bind = $"ctx.GetModalValues({id})";
                break;

            case "Checkbox" when typeName == "bool":
                component = $"new {prims}.Checkbox({id}, initial?.{property.Name})";
                bind = $"ctx.GetModalBool({id})";
                break;

            case "UserSelect" when elementType == "ulong":
            case "RoleSelect" when elementType == "ulong":
            case "ChannelSelect" when elementType == "ulong":
                int maxValues = NamedInt(attribute, "MaxValues");
                string max = isArray ? (maxValues > 0 ? maxValues : 25).ToString(CultureInfo.InvariantCulture) : "null";
                string channelTypes = kind == "ChannelSelect" ? "null, " : string.Empty;
                component = $"new {prims}.{kind}({id}, {channelTypes}{placeholder}, null, {max}) {{ required = {requiredText} }}";
                bind = isArray ? $"ctx.GetModalIds({id})" : isNullable ? $"ctx.GetModalId({id})" : $"ctx.GetModalId({id}) ?? 0UL";
                break;

            case "File" when elementType == "Attachment":
                int maxFiles = NamedInt(attribute, "MaxFiles");
                if (maxFiles == 0 && isArray) maxFiles = 10;
                string[] fileTypes = attribute.NamedArguments.Where(static a => a.Key == "FileTypes").Select(static a => ArrayArgument(a.Value)).FirstOrDefault() ?? [];
                string fileTypesText = fileTypes.Length > 0 ? "new string[] { " + string.Join(", ", fileTypes.Select(Lit)) + " }" : "null";
                component = $"new {prims}.FileUpload({id}, {requiredText}, null, {OptionalInt(maxFiles)}, {fileTypesText})";
                bind = isArray
                    ? $"ctx.GetModalFiles({id})"
                    : isNullable
                        ? $"global::System.Linq.Enumerable.FirstOrDefault(ctx.GetModalFiles({id}))"
                        : $"global::System.Linq.Enumerable.First(ctx.GetModalFiles({id}))";
                break;

            default:
                return null;
        }

        return new ModalField
        {
            PropertyName = property.Name,
            Label = label,
            Description = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Description").Value.Value as string,
            ComponentCode = component,
            BindCode = bind
        };
    }

    private static Candidate? GetCandidate(GeneratorSyntaxContext ctx)
    {
        var methodSyntax = (MethodDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol ms)
            return null;

        bool isSlash = false;
        bool isComponent = false;
        bool isAutocomplete = false;
        int? contextMenuType = null;
        string? slashName = null;
        string? slashDescription = null;
        bool autoDefer = true;
        bool methodHasNoDefer = false;
        bool deferEphemeral = false;
        ulong? requiredPermissions = null;
        string? componentId = null;
        bool componentPrefix = false;
        string? autocompleteCommandName = null;
        string? autocompleteOptionName = null;
        bool isModalHandler = false;
        bool isErrorHandler = false;
        var eventNames = new List<string>();
        var problems = new List<(string Id, string Title, string Message)>();
        string? contexts = null;
        string? integrationTypes = null;
        var localizations = new List<LocalizedText>();
        var groupLocalizations = new List<LocalizedText>();

        void ReadAvailability(string? attributeName)
        {
            if (attributeName == GuildOnlyAttr) contexts = "0";
            else if (attributeName == AllowInDmsAttr) contexts = "0, 1";
            else if (attributeName == UserInstallableAttr)
            {
                contexts = "0, 1, 2";
                integrationTypes = "0, 1";
            }
        }

        foreach (var ad in ms.GetAttributes())
        {
            var name = ad.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':');
            ReadAvailability(name);
            if (ReadLocalized(ad) is { } localized) localizations.Add(localized);

            string? eventName = ReadEventName(ad);
            if (eventName is not null)
            {
                eventNames.Add(eventName);
            }
            else if (name == OnCommandErrorAttr)
            {
                isErrorHandler = true;
            }
            else if (name == ModalHandlerAttr)
            {
                isModalHandler = true;
                isComponent = true;
            }
            else if (name == SlashAttr)
            {
                isSlash = true;
                if (ad.ConstructorArguments.Length >= 1)
                    slashName = ad.ConstructorArguments[0].Value as string;
                if (ad.ConstructorArguments.Length >= 2)
                    slashDescription = ad.ConstructorArguments[1].Value as string;
            }
            else if (name == ComponentAttr ||
                     name == ButtonHandlerAttr ||
                     name == SelectMenuHandlerAttr ||
                     name == UserSelectHandlerAttr ||
                     name == RoleSelectHandlerAttr ||
                     name == ChannelSelectHandlerAttr ||
                     name == MentionableSelectHandlerAttr)
            {
                isComponent = true;
                if (ad.ConstructorArguments.Length >= 1)
                    componentId = ad.ConstructorArguments[0].Value as string;
                if (ad.ConstructorArguments.Length >= 2 && ad.ConstructorArguments[1].Value is bool b)
                    componentPrefix = b;
            }
            else if (name == NoDeferAttr)
            {
                methodHasNoDefer = true;
                autoDefer = false; // [NoDefer] disables auto-defer
            }
            else if (name == EphemeralAttr)
            {
                deferEphemeral = true; // [Ephemeral] makes auto-defer use ephemeral: true
            }
            else if (name == RequirePermissionsAttr)
            {
                if (ad.ConstructorArguments.Length >= 1)
                {
                    var val = ad.ConstructorArguments[0].Value;
                    if (val is ulong u)
                        requiredPermissions = u;
                    else if (val is long l)
                        requiredPermissions = (ulong)l;
                    else if (val is int i)
                        requiredPermissions = (ulong)i;
                }
            }
            else if (name == UserContextMenuAttr)
            {
                contextMenuType = 2;
                if (ad.ConstructorArguments.Length >= 1)
                    slashName = ad.ConstructorArguments[0].Value as string;
            }
            else if (name == MessageContextMenuAttr)
            {
                contextMenuType = 3;
                if (ad.ConstructorArguments.Length >= 1)
                    slashName = ad.ConstructorArguments[0].Value as string;
            }
            else if (name == AutocompleteAttr)
            {
                isAutocomplete = true;
                if (ad.ConstructorArguments.Length >= 1)
                    autocompleteCommandName = ad.ConstructorArguments[0].Value as string;
                if (ad.ConstructorArguments.Length >= 2)
                    autocompleteOptionName = ad.ConstructorArguments[1].Value as string;
            }
        }

        if (!isSlash && !isComponent && contextMenuType is null && !isAutocomplete && eventNames.Count == 0 && !isErrorHandler) return null;

        // Group attributes on containing type
        string? groupName = null;
        string? groupDescription = null;
        string? subGroupName = null;
        string? subGroupDescription = null;
        if (ms.ContainingType is { } ct)
        {
            foreach (var ad in ct.GetAttributes())
            {
                var name = ad.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':');
                if (contexts is null) ReadAvailability(name);
                if (ReadLocalized(ad) is { } localized) groupLocalizations.Add(localized);

                if (name == SlashGroupAttr)
                {
                    if (ad.ConstructorArguments.Length >= 1)
                        groupName = ad.ConstructorArguments[0].Value as string;
                    if (ad.ConstructorArguments.Length >= 2)
                        groupDescription = ad.ConstructorArguments[1].Value as string;
                }
                else if (name == SlashSubGroupAttr)
                {
                    if (ad.ConstructorArguments.Length >= 1)
                        subGroupName = ad.ConstructorArguments[0].Value as string;
                    if (ad.ConstructorArguments.Length >= 2)
                        subGroupDescription = ad.ConstructorArguments[1].Value as string;
                }
                // Check for [NoDefer] on the class level (only if method didn't have explicit [NoDefer])
                else if (name == NoDeferAttr && !methodHasNoDefer)
                {
                    autoDefer = false; // [NoDefer] disables auto-defer
                }
                // Check for [Ephemeral] on the class level
                else if (name == EphemeralAttr && !deferEphemeral)
                {
                    deferEphemeral = true; // Class-level [Ephemeral]
                }
            }
        }

        // Detect whether the first parameter is our InteractionContext type. Be tolerant to Roslyn's
        // fully-qualified formatting (global:: prefix) and nullable annotations.
        bool hasContext = false;
        int paramStartIndex = 0;
        if (ms.Parameters.Length > 0)
        {
            var p0 = ms.Parameters[0].Type;
            var p0Display = p0.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':')
                .TrimEnd('?');
            if (p0Display == "SimpleDiscordNet.Commands.InteractionContext")
            {
                hasContext = true;
                paramStartIndex = 1; // Options start after context
            }
            else if (p0.Name == "InteractionContext")
            {
                // Fallback: rely on short name match to be resilient if formats differ
                hasContext = true;
                paramStartIndex = 1;
            }
        }

        // Event handlers take the event's data, or nothing
        bool eventTakesData = ms.Parameters.Length == 1;
        if (eventNames.Count > 0)
        {
            var events = ctx.SemanticModel.Compilation.GetTypeByMetadataName(DiscordEventsType);
            foreach (string eventName in eventNames)
            {
                var symbol = events?.GetMembers(eventName).OfType<IEventSymbol>().FirstOrDefault(static e => e.DeclaredAccessibility == Accessibility.Public);
                var expected = (symbol?.Type as INamedTypeSymbol)?.TypeArguments.FirstOrDefault();
                string shown = $"{ms.ContainingType.Name}.{ms.Name}";

                if (symbol is null)
                {
                    problems.Add(("SDN011", "Unknown event", $"Method '{shown}' listens for '{eventName}', which isn't an event on DiscordEvents."));
                }
                else if (ms.Parameters.Length > 1 || (eventTakesData && (expected is null || !SymbolEqualityComparer.Default.Equals(WithoutNullable(ms.Parameters[0].Type), WithoutNullable(expected)))))
                {
                    string wanted = expected?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "no";
                    problems.Add(("SDN011", "Wrong event handler parameters", $"Method '{shown}' handles '{eventName}', so it should take {wanted} parameter or none."));
                }
            }
        }

        if (isErrorHandler && (ms.Parameters.Length != 2
            || ms.Parameters[0].Type.ToDisplayString().TrimEnd('?') != InteractionContextType
            || ms.Parameters[1].Type.ToDisplayString().TrimEnd('?') != "System.Exception"))
        {
            problems.Add(("SDN012", "Wrong error handler parameters", $"Method '{ms.ContainingType.Name}.{ms.Name}' uses [OnCommandError], so it must take an InteractionContext and an Exception."));
        }

        // Parameters of a component handler bind to the named parts of its ID
        var templateArguments = new List<string>();
        bool isTemplate = isComponent && !isModalHandler && componentId?.Contains('{') == true;
        if (isTemplate)
        {
            var names = TemplateNames(componentId!);
            foreach (var parameter in ms.Parameters.Skip(paramStartIndex))
            {
                string? code = names.Contains(parameter.Name) ? TemplateValueCode(parameter.Name, WithoutNullable(parameter.Type)) : null;
                if (code is null)
                {
                    problems.Add(("SDN010", "Unsupported component ID parameter",
                        $"Parameter '{parameter.Name}' of '{ms.ContainingType.Name}.{ms.Name}' must match a part of \"{componentId}\" and be a string, int, long, ulong, bool or enum."));
                    continue;
                }
                templateArguments.Add(code);
            }
        }

        // A modal handler's form is the parameter whose class is marked [Modal]
        string? modalFormType = null;
        if (isModalHandler)
        {
            var formParameter = ms.Parameters.Skip(paramStartIndex).FirstOrDefault();
            var modal = formParameter?.Type.GetAttributes().FirstOrDefault(a => AttributeName(a) == ModalAttr);
            if (formParameter is not null && modal is not null)
            {
                modalFormType = formParameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                componentId = (modal.NamedArguments.FirstOrDefault(a => a.Key == "Id").Value.Value as string) ?? formParameter.Type.Name;
            }
        }

        // Parse command option parameters (for slash commands only)
        var options = new List<OptionParameter>();
        var unsupportedParameters = new List<string>();
        if (isSlash)
        {
            for (int i = paramStartIndex; i < ms.Parameters.Length; i++)
            {
                var param = ms.Parameters[i];
                var optAttr = param.GetAttributes().FirstOrDefault(a =>
                {
                    var name = a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':');
                    return name == CommandOptionAttr;
                });

                // Support parameters with or without [CommandOption] for backward compatibility
                string? optionName = null;
                string? description = null;
                bool? explicitRequired = null;
                int? minLength = null;
                int? maxLength = null;
                double? minValue = null;
                double? maxValue = null;
                string? channelTypes = null;
                string? choices = null;
                bool autocomplete = false;

                if (optAttr is not null)
                {
                    // Parse attribute values
                    optionName = optAttr.ConstructorArguments.Length >= 1 ? optAttr.ConstructorArguments[0].Value as string : null;
                    description = optAttr.ConstructorArguments.Length >= 2 ? optAttr.ConstructorArguments[1].Value as string : null;

                    // Parse all named arguments from attribute
                    foreach (var namedArg in optAttr.NamedArguments)
                    {
                        switch (namedArg.Key)
                        {
                            case "Required" when namedArg.Value.Value is bool reqVal:
                                explicitRequired = reqVal;
                                break;
                            case "MinLength" when namedArg.Value.Value is int minLen:
                                minLength = minLen;
                                break;
                            case "MaxLength" when namedArg.Value.Value is int maxLen:
                                maxLength = maxLen;
                                break;
                            case "MinValue" when namedArg.Value.Value is double minVal:
                                minValue = minVal;
                                break;
                            case "MaxValue" when namedArg.Value.Value is double maxVal:
                                maxValue = maxVal;
                                break;
                            case "ChannelTypes" when namedArg.Value.Value is string chanTypes:
                                channelTypes = chanTypes;
                                break;
                            case "Choices" when namedArg.Value.Value is string ch:
                                choices = ch;
                                break;
                            case "Autocomplete" when namedArg.Value.Value is bool auto:
                                autocomplete = auto;
                                break;
                        }
                    }

                }

                // Parse CommandChoice attributes (takes precedence over Choices property)
                var choiceAttrs = param.GetAttributes().Where(a =>
                {
                    var name = a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':');
                    return name == CommandChoiceAttr;
                }).ToList();

                if (choiceAttrs.Count > 0)
                {
                    // Build choices string from CommandChoice attributes
                    var choiceList = new List<string>();
                    foreach (var choiceAttr in choiceAttrs)
                    {
                        if (choiceAttr.ConstructorArguments.Length >= 2)
                        {
                            string? displayName = choiceAttr.ConstructorArguments[0].Value as string;
                            object? value = choiceAttr.ConstructorArguments[1].Value;
                            if (!string.IsNullOrWhiteSpace(displayName) && value != null)
                            {
                                choiceList.Add($"{displayName}:{FormatDefaultValue(value)}");
                            }
                        }
                    }
                    if (choiceList.Count > 0)
                    {
                        choices = string.Join(",", choiceList);
                    }
                }

                // Set defaults for parameters without [CommandOption] attribute
                if (string.IsNullOrWhiteSpace(optionName)) optionName = param.Name;
                if (string.IsNullOrWhiteSpace(description)) description = "option";

                var paramType = param.Type;
                bool isNullable = paramType.NullableAnnotation == NullableAnnotation.Annotated;
                string typeName = paramType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat).TrimEnd('?');
                ITypeSymbol coreType = WithoutNullable(paramType);
                bool isEnum = coreType.TypeKind == TypeKind.Enum;

                // Map C# types to Discord option types - support primitives and entity types.
                // ulong is sent as a string option because Discord integers stop at 2^53, below snowflake range.
                string discordType = isEnum ? "Enum" : typeName switch
                {
                    "string" => "string",
                    "long" => "long",
                    "ulong" => "ulong",
                    "int" => "int",
                    "bool" => "bool",
                    "double" => "double",
                    "float" => "float",
                    "DiscordUser" => "User",
                    "DiscordMember" => "Member",
                    "DiscordChannel" => "Channel",
                    "DiscordRole" => "Role",
                    "Attachment" => "Attachment",
                    _ => "unknown"
                };

                if (discordType == "unknown")
                {
                    unsupportedParameters.Add($"{param.Name} ({typeName})");
                    continue;
                }

                if (isEnum)
                {
                    const int maxChoices = 25;
                    var members = EnumMembers(coreType);
                    if (members.Count is 0 or > maxChoices)
                    {
                        problems.Add(("SDN009", "Enum doesn't fit Discord's choices",
                            $"Parameter '{param.Name}' of '{ms.ContainingType.Name}.{ms.Name}' uses enum '{coreType.Name}' with {members.Count} values. Discord allows 1 to {maxChoices} choices."));
                        continue;
                    }

                    // Choices from [CommandChoice] still take priority
                    choices ??= string.Join(",", members.Select(static m => $"{SpacedName(m)}:{m}"));
                }

                bool isRequired = explicitRequired ?? (!isNullable && !param.HasExplicitDefaultValue);
                string? defaultValue = DefaultCode(param, coreType);
                string? enumExtraction = null;
                if (isEnum)
                {
                    string enumFallback = defaultValue ?? (isNullable ? "null" : $"default({coreType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})");
                    enumExtraction = EnumFromString($"FindOption(_opts, \"{NormalizeName(optionName!)}\")?.String", coreType, enumFallback, isNullable);
                }

                options.Add(new OptionParameter
                {
                    ParameterName = param.Name,
                    OptionName = optionName!,
                    Description = description!,
                    TypeName = discordType,
                    ActualTypeName = typeName, // Store the actual C# type (e.g., "ulong")
                    IsNullable = isNullable,
                    IsRequired = isRequired,
                    DefaultValue = defaultValue,
                    MinLength = minLength,
                    MaxLength = maxLength,
                    MinValue = minValue,
                    MaxValue = maxValue,
                    ChannelTypes = channelTypes,
                    Choices = choices,
                    Autocomplete = autocomplete,
                    EnumExtraction = enumExtraction,
                    Localizations = param.GetAttributes().Select(ReadLocalized).Where(static l => l is not null).Select(static l => l!).ToList()
                });
            }
        }

        // Only parameterless instance or any static methods are supported
        bool isStatic = ms.IsStatic;
        bool hasDefaultCtor = false;
        var injectedTypes = new List<string>();
        if (!isStatic)
        {
            var publicCtors = ms.ContainingType.InstanceConstructors.Where(static c => c.DeclaredAccessibility == Accessibility.Public).ToList();
            hasDefaultCtor = publicCtors.Any(static c => c.Parameters.Length == 0);

            // Without a parameterless constructor, the largest one receives its arguments from the app's services
            var injected = publicCtors.OrderByDescending(static c => c.Parameters.Length).FirstOrDefault();
            if (!hasDefaultCtor && injected is not null && injected.Parameters.All(static p => p.Type.IsReferenceType))
            {
                hasDefaultCtor = true;
                injectedTypes.AddRange(injected.Parameters.Select(static p => p.Type.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            }
        }

        var (returnKind, replyKind) = ReadReturn(ms);

        return new Candidate
        {
            Namespace = GetNamespace(ms.ContainingType),
            TypeName = ms.ContainingType.Name,
            ContainingType = ms.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':'),
            MethodName = ms.Name,
            IsStatic = isStatic,
            HasDefaultCtor = hasDefaultCtor,
            HasContext = hasContext,
            IsSlash = isSlash,
            IsAutocomplete = isAutocomplete,
            ContextMenuType = contextMenuType,
            SlashName = slashName,
            SlashDescription = slashDescription,
            GroupName = groupName,
            GroupDescription = groupDescription,
            SubGroupName = subGroupName,
            SubGroupDescription = subGroupDescription,
            AutoDefer = autoDefer,
            DeferEphemeral = deferEphemeral,
            RequiredPermissions = requiredPermissions,
            IsComponent = isComponent,
            ComponentId = componentId,
            ComponentPrefix = componentPrefix,
            AutocompleteCommandName = autocompleteCommandName,
            AutocompleteOptionName = autocompleteOptionName,
            Options = options,
            UnsupportedParameters = unsupportedParameters,
            ReturnKind = returnKind,
            ReplyKind = replyKind,
            Location = ms.Locations.FirstOrDefault() ?? Location.None,
            Checks = ReadChecks(ms),
            InjectedTypes = injectedTypes,
            EventNames = eventNames,
            EventTakesData = eventTakesData,
            IsErrorHandler = isErrorHandler,
            IsTemplate = isTemplate,
            TemplateArguments = templateArguments,
            Problems = problems,
            IsModalHandler = isModalHandler,
            ModalFormType = modalFormType,
            Contexts = contexts,
            IntegrationTypes = integrationTypes,
            Localizations = localizations,
            GroupLocalizations = groupLocalizations
        };
    }

    private static string GetNamespace(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace;
        if (ns is null || ns.IsGlobalNamespace) return string.Empty;
        return ns.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':');
    }

    private static string BuildLocalizationFields(List<LocalizedText> localizations, bool normalizeNames, bool includeDescriptions)
    {
        if (localizations.Count == 0) return string.Empty;

        const string dictionary = "new global::System.Collections.Generic.Dictionary<string, string>";
        var sb = new StringBuilder();
        var names = localizations.Select(l => $"[{Lit(l.Locale)}] = {Lit(normalizeNames ? NormalizeName(l.Name) : l.Name)}");
        sb.Append($", name_localizations = {dictionary} {{ {string.Join(", ", names)} }}");

        var descriptions = localizations.Where(static l => l.Description is not null).ToList();
        if (includeDescriptions && descriptions.Count > 0)
        {
            var entries = descriptions.Select(static l => $"[{Lit(l.Locale)}] = {Lit(l.Description)}");
            sb.Append($", description_localizations = {dictionary} {{ {string.Join(", ", entries)} }}");
        }

        return sb.ToString();
    }

    private static string BuildAvailabilityFields(string? contexts, string? integrationTypes)
    {
        string result = string.Empty;
        if (contexts is not null) result += $", contexts = new int[] {{ {contexts} }}";
        if (integrationTypes is not null) result += $", integration_types = new int[] {{ {integrationTypes} }}";
        return result;
    }

    private static string ModalHelperName(string formType) => $"__ModalForm_{SanitizeId(formType)}";

    private static void ReportError(SourceProductionContext spc, Location location, string id, string title, string message, params object?[] args)
    {
        spc.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
            id: id,
            title: title,
            messageFormat: message,
            category: "SimpleDiscordNet",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true), location, args));
    }

    private static string EventHandlerCode(Candidate handler, string eventName)
    {
        string target = handler.IsStatic ? handler.ContainingType : $"__InstHolder_{SanitizeId(handler.ContainingType)}.Value";
        string call = $"{target}.{handler.MethodName}({(handler.EventTakesData ? "_e" : string.Empty)})";
        const string events = "global::SimpleDiscordNet.Commands.GeneratedEvents";
        return $"{events}.Subscribe(static () => global::SimpleDiscordNet.Events.DiscordEvents.{eventName} += (_, _e) => {events}.Run(async () => {{ {CallCode(handler, call, allowReply: false)} }}));";
    }

    private static string ErrorHandlerCode(Candidate handler)
    {
        string target = handler.IsStatic ? handler.ContainingType : $"__InstHolder_{SanitizeId(handler.ContainingType)}.Value";
        return $"global::SimpleDiscordNet.Commands.CommandErrorHandlers.Register(async (ctx, _error) => {{ {CallCode(handler, $"{target}.{handler.MethodName}(ctx, _error)", allowReply: false)} }});";
    }

    private static void EmitModalForm(StringBuilder sb, ModalFormCandidate form)
    {
        const string prims = "global::SimpleDiscordNet.Primitives";
        sb.AppendLine($"    internal static class {ModalHelperName(form.TypeName)}");
        sb.AppendLine("    {");
        sb.AppendLine($"        public static {prims}.IComponent[] Build({form.TypeName}? initial) => new {prims}.IComponent[]");
        sb.AppendLine("        {");
        foreach (var field in form.Fields)
        {
            sb.AppendLine($"            new {prims}.Label({Lit(field.Label)}, {field.ComponentCode}, {Lit(field.Description)}),");
        }
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine($"        public static {form.TypeName} Bind(global::SimpleDiscordNet.Commands.InteractionContext ctx) => new {form.TypeName}");
        sb.AppendLine("        {");
        foreach (var field in form.Fields)
        {
            sb.AppendLine($"            {field.PropertyName} = {field.BindCode},");
        }
        sb.AppendLine("        };");
        sb.AppendLine("    }");
    }

    private static void Emit(SourceProductionContext spc, string assemblyName, IReadOnlyList<Candidate> candidates, IReadOnlyList<ModalFormCandidate> allForms, AttributeData? help)
    {
        foreach (var candidate in candidates)
        {
            foreach (var (id, title, message) in candidate.Problems)
            {
                ReportError(spc, candidate.Location, id, title, "{0}", message);
            }
        }

        var errorHandlers = candidates.Where(static c => c.IsErrorHandler).ToList();
        foreach (var extra in errorHandlers.Skip(1))
        {
            ReportError(spc, extra.Location, "SDN012", "Too many error handlers", "An app can only have one [OnCommandError] method. '{0}.{1}' is an extra one.", extra.TypeName, extra.MethodName);
        }

        const int maxModalFields = 5;
        var forms = new List<ModalFormCandidate>();
        foreach (var form in allForms)
        {
            if (form.Problems.Count > 0)
            {
                ReportError(spc, form.Location, "SDN006", "Unsupported modal field",
                    "Modal form '{0}' has unsupported field(s): {1}. Check that each property has a setter and that its type suits the field attribute.",
                    form.DisplayName, string.Join(", ", form.Problems));
            }
            else if (form.Fields.Count is 0 or > maxModalFields)
            {
                ReportError(spc, form.Location, "SDN007", "Wrong number of modal fields",
                    "Modal form '{0}' has {1} field(s). A modal needs 1 to {2}.", form.DisplayName, form.Fields.Count, maxModalFields);
            }
            else if (!form.HasDefaultCtor)
            {
                ReportError(spc, form.Location, "SDN008", "Modal form needs a parameterless constructor",
                    "Modal form '{0}' needs a parameterless constructor so the generator can fill it in.", form.DisplayName);
            }
            else
            {
                forms.Add(form);
            }
        }

        foreach (var handler in candidates.Where(static c => c.IsModalHandler && c.ModalFormType is null))
        {
            ReportError(spc, handler.Location, "SDN005", "Modal handler needs a form parameter",
                "Method '{0}.{1}' uses [ModalHandler] but has no parameter of a class marked [Modal].", handler.ContainingType, handler.MethodName);
        }

        var eventHandlers = candidates.Where(static c => c.EventNames.Count > 0 && c.Problems.Count == 0).ToList();
        var commands = candidates.Where(c => c.IsSlash && !string.IsNullOrWhiteSpace(c.SlashName)).ToList();
        var contextMenus = candidates.Where(c => c.ContextMenuType.HasValue && !string.IsNullOrWhiteSpace(c.SlashName)).ToList();
        var components = candidates.Where(c => c.IsComponent && !string.IsNullOrWhiteSpace(c.ComponentId)).ToList();
        var autocompletes = candidates.Where(c => c.IsAutocomplete && !string.IsNullOrWhiteSpace(c.AutocompleteCommandName) && !string.IsNullOrWhiteSpace(c.AutocompleteOptionName)).ToList();

        foreach (var c in candidates.Where(static c => c.UnsupportedParameters.Count > 0))
        {
            ReportError(spc, c.Location, "SDN004", "Unsupported slash command parameter type",
                "Slash command method '{0}.{1}' has unsupported parameter(s): {2}. Supported types are string, long, ulong, int, bool, double, float, enums, DiscordUser, DiscordMember, DiscordChannel, DiscordRole and Attachment.",
                c.ContainingType, c.MethodName, string.Join(", ", c.UnsupportedParameters));
        }

        // Organize slash commands
        var ungrouped = new List<Candidate>();
        var grouped = new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);
        var subGrouped = new Dictionary<string, Dictionary<string, List<Candidate>>>(StringComparer.Ordinal);

        foreach (var c in commands)
        {
            var normName = NormalizeName(c.SlashName!);
            c.SlashName = normName;
            if (!string.IsNullOrWhiteSpace(c.SubGroupName) && string.IsNullOrWhiteSpace(c.GroupName))
            {
                ReportError(spc, c.Location, "SDN002", "Subcommand group requires a top-level command group",
                    "Type '{0}' uses SlashCommandSubGroupAttribute but does not declare a SlashCommandGroupAttribute.", c.ContainingType);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(c.GroupName))
            {
                var grp = NormalizeName(c.GroupName!);
                c.GroupName = grp;
                if (!string.IsNullOrWhiteSpace(c.SubGroupName))
                {
                    var subGrp = NormalizeName(c.SubGroupName!);
                    c.SubGroupName = subGrp;
                    if (!subGrouped.TryGetValue(grp, out var groupDict))
                    {
                        groupDict = new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);
                        subGrouped[grp] = groupDict;
                    }
                    if (!groupDict.TryGetValue(subGrp, out var list))
                    {
                        list = new List<Candidate>();
                        groupDict[subGrp] = list;
                    }
                    list.Add(c);
                }
                else
                {
                    if (!grouped.TryGetValue(grp, out var list))
                    {
                        list = new List<Candidate>();
                        grouped[grp] = list;
                    }
                    list.Add(c);
                }
            }
            else
            {
                ungrouped.Add(c);
            }
        }

        // Build definitions array source
        var defsBuilder = new StringBuilder();
        defsBuilder.AppendLine("new global::SimpleDiscordNet.Models.ApplicationCommandDefinition[] {");
        var topLevelNames = new HashSet<string>(grouped.Keys, StringComparer.Ordinal);
        topLevelNames.UnionWith(subGrouped.Keys);
        foreach (var topName in topLevelNames)
        {
            grouped.TryGetValue(topName, out var direct);
            subGrouped.TryGetValue(topName, out var subGroups);

            var groupCandidate = direct?.FirstOrDefault()
                ?? subGroups?.Values.FirstOrDefault()?.FirstOrDefault();
            if (groupCandidate is null) continue;

            defsBuilder.AppendLine("    new global::SimpleDiscordNet.Models.ApplicationCommandDefinition { ");
            defsBuilder.AppendLine($"        name = \"{topName}\", ");
            defsBuilder.AppendLine("        type = 1,");
            var gdesc = !string.IsNullOrWhiteSpace(groupCandidate.GroupDescription) ? groupCandidate.GroupDescription!.Replace("\"", "\\\"") : "group";
            defsBuilder.AppendLine($"        description = \"{gdesc}\",");

            // Add default_member_permissions if any command under this top-level has RequiredPermissions
            var groupPerms = direct?.FirstOrDefault(c => c.RequiredPermissions.HasValue)?.RequiredPermissions
                             ?? subGroups?.SelectMany(g => g.Value).FirstOrDefault(c => c.RequiredPermissions.HasValue)?.RequiredPermissions;
            if (groupPerms.HasValue)
            {
                defsBuilder.AppendLine($"        default_member_permissions = \"{groupPerms.Value.ToString(CultureInfo.InvariantCulture)}\",");
            }

            // Availability and translations belong to the top-level command, so one marked command sets them for the group
            var allInGroup = (direct ?? Enumerable.Empty<Candidate>()).Concat(subGroups?.SelectMany(g => g.Value) ?? Enumerable.Empty<Candidate>()).ToList();
            var groupAvailability = allInGroup.FirstOrDefault(static c => c.Contexts is not null);
            string groupFields = BuildAvailabilityFields(groupAvailability?.Contexts, groupAvailability?.IntegrationTypes)
                + BuildLocalizationFields(groupCandidate.GroupLocalizations, normalizeNames: true, includeDescriptions: true);
            if (groupFields.Length > 0)
            {
                defsBuilder.AppendLine($"        {groupFields.TrimStart(',', ' ')},");
            }

            defsBuilder.AppendLine("        options = new global::SimpleDiscordNet.Models.ApplicationCommandDefinition[] {");

            if (direct is not null)
            {
                var seen = new HashSet<(string, string)>();
                foreach (var sc in direct)
                {
                    var key = (sc.SlashName!, sc.SlashDescription ?? "command");
                    if (!seen.Add(key))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                            id: "SDN003",
                            title: "Duplicate slash command name",
                            messageFormat: "Duplicate slash command name '{0}' found in the same group/subgroup.",
                            category: "SimpleDiscordNet",
                            DiagnosticSeverity.Warning,
                            isEnabledByDefault: true),
                            sc.Location,
                            sc.SlashName));
                        continue;
                    }

                    var desc = string.IsNullOrWhiteSpace(sc.SlashDescription) ? "command" : sc.SlashDescription!.Replace("\"", "\\\"");
                    var optsArray = BuildOptionsArray(sc.Options);
                    var scPerms = sc.RequiredPermissions.HasValue ? $", default_member_permissions = \"{sc.RequiredPermissions.Value.ToString(CultureInfo.InvariantCulture)}\"" : "";
                    var scText = BuildLocalizationFields(sc.Localizations, normalizeNames: true, includeDescriptions: true);
                    defsBuilder.AppendLine($"            new global::SimpleDiscordNet.Models.ApplicationCommandDefinition {{ name = \"{sc.SlashName}\", type = 1, description = \"{desc}\", options = {optsArray}{scPerms}{scText} }},");
                }
            }

            if (subGroups is not null)
            {
                foreach (var sg in subGroups)
                {
                    var sgdesc = sg.Value.Count > 0 && !string.IsNullOrWhiteSpace(sg.Value[0]?.SubGroupDescription)
                        ? sg.Value[0].SubGroupDescription!.Replace("\"", "\\\"")
                        : "group";

                    defsBuilder.AppendLine($"            new global::SimpleDiscordNet.Models.ApplicationCommandDefinition {{ name = \"{sg.Key}\", type = 2, description = \"{sgdesc}\", options = new global::SimpleDiscordNet.Models.ApplicationCommandDefinition[] {{");
                    var seen = new HashSet<(string, string)>();
                    foreach (var sc in sg.Value)
                    {
                        var key = (sc.SlashName!, sc.SlashDescription ?? "command");
                        if (!seen.Add(key))
                        {
                            spc.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                                id: "SDN003",
                                title: "Duplicate slash command name",
                                messageFormat: "Duplicate slash command name '{0}' found in the same group/subgroup.",
                                category: "SimpleDiscordNet",
                                DiagnosticSeverity.Warning,
                                isEnabledByDefault: true),
                                sc.Location,
                                sc.SlashName));
                            continue;
                        }

                        var desc = string.IsNullOrWhiteSpace(sc.SlashDescription) ? "command" : sc.SlashDescription!.Replace("\"", "\\\"");
                        var optsArray = BuildOptionsArray(sc.Options);
                        var scPerms = sc.RequiredPermissions.HasValue ? $", default_member_permissions = \"{sc.RequiredPermissions.Value.ToString(CultureInfo.InvariantCulture)}\"" : "";
                        var scText = BuildLocalizationFields(sc.Localizations, normalizeNames: true, includeDescriptions: true);
                        defsBuilder.AppendLine($"                new global::SimpleDiscordNet.Models.ApplicationCommandDefinition {{ name = \"{sc.SlashName}\", type = 1, description = \"{desc}\", options = {optsArray}{scPerms}{scText} }},");
                    }
                    defsBuilder.AppendLine("            }},");
                }
            }

            defsBuilder.AppendLine("        }");
            defsBuilder.AppendLine("    },");
        }

        var seenUngrouped = new HashSet<(string, string)>();
        foreach (var u in ungrouped)
        {
            var key = (u.SlashName!, u.SlashDescription ?? "command");
            if (!seenUngrouped.Add(key)) continue;

            var desc = string.IsNullOrWhiteSpace(u.SlashDescription) ? "command" : u.SlashDescription!.Replace("\"", "\\\"");
            var optsArray = BuildOptionsArray(u.Options);
            var permsField = u.RequiredPermissions.HasValue ? $", default_member_permissions = \"{u.RequiredPermissions.Value.ToString(CultureInfo.InvariantCulture)}\"" : "";
            var extraFields = BuildAvailabilityFields(u.Contexts, u.IntegrationTypes) + BuildLocalizationFields(u.Localizations, normalizeNames: true, includeDescriptions: true);
            defsBuilder.AppendLine($"    new global::SimpleDiscordNet.Models.ApplicationCommandDefinition {{ name = \"{u.SlashName}\", type = 1, description = \"{desc}\", options = {optsArray}{permsField}{extraFields} }},");
        }

        // The command added by [assembly: GenerateHelp], unless the app already has one with that name
        string? helpName = null;
        string helpTitle = "Commands";
        if (help is not null)
        {
            string requested = NormalizeName(help.NamedArguments.FirstOrDefault(a => a.Key == "Name").Value.Value as string ?? "help");
            if (ungrouped.All(u => u.SlashName != requested))
            {
                helpName = requested;
                helpTitle = help.NamedArguments.FirstOrDefault(a => a.Key == "Title").Value.Value as string ?? helpTitle;
                string helpDescription = help.NamedArguments.FirstOrDefault(a => a.Key == "Description").Value.Value as string ?? "List the bot's commands";
                defsBuilder.AppendLine($"    new global::SimpleDiscordNet.Models.ApplicationCommandDefinition {{ name = {Lit(helpName)}, type = 1, description = {Lit(helpDescription)} }},");
            }
        }

        // Context menu commands (type 2 = User, type 3 = Message)
        var seenContextMenus = new HashSet<(string, int)>();
        foreach (var cm in contextMenus)
        {
            if (!cm.ContextMenuType.HasValue) continue;
            var key = (cm.SlashName!, cm.ContextMenuType.Value);
            if (!seenContextMenus.Add(key)) continue;

            // Discord requires an empty description for user and message commands
            var permsField = cm.RequiredPermissions.HasValue ? $", default_member_permissions = \"{cm.RequiredPermissions.Value.ToString(CultureInfo.InvariantCulture)}\"" : "";
            var extraFields = BuildAvailabilityFields(cm.Contexts, cm.IntegrationTypes) + BuildLocalizationFields(cm.Localizations, normalizeNames: false, includeDescriptions: false);
            defsBuilder.AppendLine($"    new global::SimpleDiscordNet.Models.ApplicationCommandDefinition {{ name = \"{cm.SlashName}\", type = {cm.ContextMenuType.Value.ToString(CultureInfo.InvariantCulture)}, description = \"\"{permsField}{extraFields} }},");
        }
        defsBuilder.AppendLine("}");

        // Build handler dictionaries
        string BuildHandlerFactory(Candidate c)
        {
            var targetExpr = c.IsStatic ? c.ContainingType : $"__InstHolder_{SanitizeId(c.ContainingType)}.Value";

            // Generate parameter binding code for options
            var bindingCode = new StringBuilder();
            var paramList = new List<string>();

            if (c.HasContext)
            {
                paramList.Add("ctx");
            }

            if (c.Options.Count > 0)
            {
                bindingCode.Append("var _opts = ctx.Command?.Options ?? (global::System.Collections.Generic.IReadOnlyList<global::SimpleDiscordNet.Models.InteractionOption>)System.Array.Empty<global::SimpleDiscordNet.Models.InteractionOption>(); ");

                foreach (var opt in c.Options)
                {
                    string normalizedName = NormalizeName(opt.OptionName);
                    string varName = $"_param_{opt.ParameterName}";
                    string option = $"FindOption(_opts, \"{normalizedName}\")";

                    // Discord sends entity options as an ID, with the entity itself in the interaction's resolved data
                    string? entityExtraction = opt.TypeName switch
                    {
                        "User" => $"{option}?.String is string _id_{opt.ParameterName} ? ctx.GetResolvedUser(_id_{opt.ParameterName}) : null",
                        "Member" => $"{option}?.String is string _id_{opt.ParameterName} ? ctx.GetResolvedMember(_id_{opt.ParameterName}) : null",
                        "Channel" => $"{option}?.String is string _id_{opt.ParameterName} ? ctx.GetResolvedChannel(_id_{opt.ParameterName}) : null",
                        "Role" => $"{option}?.String is string _id_{opt.ParameterName} ? ctx.GetResolvedRole(_id_{opt.ParameterName}) : null",
                        "Attachment" => $"{option}?.String is string _id_{opt.ParameterName} ? ctx.GetResolvedAttachment(_id_{opt.ParameterName}) : null",
                        _ => null
                    };

                    // An omitted option falls back to the parameter's default value, if it has one
                    string Fallback(string normal) => opt.DefaultValue ?? normal;

                    // Generate extraction code based on type and nullability
                    string extraction;

                    if (entityExtraction is not null)
                    {
                        extraction = entityExtraction;
                    }
                    else if (opt.EnumExtraction is not null)
                    {
                        extraction = opt.EnumExtraction;
                    }
                    else if (opt.IsNullable && opt.DefaultValue is not null)
                    {
                        extraction = opt.ActualTypeName switch
                        {
                            "string" => $"{option}?.String ?? {opt.DefaultValue}",
                            "long" => $"{option}?.Integer ?? {opt.DefaultValue}",
                            "ulong" => $"ulong.TryParse({option}?.String, global::System.Globalization.NumberStyles.None, global::System.Globalization.CultureInfo.InvariantCulture, out ulong _ulongVal_{opt.ParameterName}) ? _ulongVal_{opt.ParameterName} : (ulong?){opt.DefaultValue}",
                            "int" => $"{option}?.Integer is long _intVal_{opt.ParameterName} ? (int)_intVal_{opt.ParameterName} : (int?){opt.DefaultValue}",
                            "bool" => $"{option}?.Boolean ?? {opt.DefaultValue}",
                            "double" => $"{option}?.Number ?? {opt.DefaultValue}",
                            "float" => $"(float?)({option}?.Number ?? {opt.DefaultValue})",
                            _ => "null"
                        };
                    }
                    else if (opt.IsNullable)
                    {
                        // Nullable types - return nullable values
                        extraction = opt.ActualTypeName switch
                        {
                            "string" => $"{option}?.String",
                            "long" => $"{option}?.Integer",
                            "ulong" => $"ulong.TryParse({option}?.String, global::System.Globalization.NumberStyles.None, global::System.Globalization.CultureInfo.InvariantCulture, out ulong _ulongVal_{opt.ParameterName}) ? _ulongVal_{opt.ParameterName} : (ulong?)null",
                            "int" => $"{option}?.Integer is long _intVal_{opt.ParameterName} ? (int)_intVal_{opt.ParameterName} : (int?)null",
                            "bool" => $"{option}?.Boolean",
                            "double" => $"{option}?.Number",
                            "float" => $"(float?){option}?.Number",
                            _ => "null"
                        };
                    }
                    else
                    {
                        // Non-nullable types - provide defaults for value types
                        extraction = opt.ActualTypeName switch
                        {
                            "string" => $"{option}?.String",
                            "long" => $"{option}?.Integer ?? {Fallback("0L")}",
                            "ulong" => $"ulong.TryParse({option}?.String, global::System.Globalization.NumberStyles.None, global::System.Globalization.CultureInfo.InvariantCulture, out ulong _ulongVal_{opt.ParameterName}) ? _ulongVal_{opt.ParameterName} : (ulong){Fallback("0UL")}",
                            "int" => $"(int)({option}?.Integer ?? {Fallback("0L")})",
                            "bool" => $"{option}?.Boolean ?? {Fallback("false")}",
                            "double" => $"{option}?.Number ?? {Fallback("0.0")}",
                            "float" => $"(float)({option}?.Number ?? {Fallback("0.0")})",
                            _ => "null"
                        };
                    }

                    // Add parameter binding with proper nullability handling
                    if (opt.IsNullable)
                    {
                        // Nullable types - extraction already returns nullable, no operator needed
                        bindingCode.Append($"var {varName} = {extraction}; ");
                    }
                    else if (entityExtraction is not null)
                    {
                        // The conditional must be parenthesized before ?? can apply to it
                        bindingCode.Append($"var {varName} = ({extraction}) ?? throw new global::System.InvalidOperationException(\"Required parameter '{opt.ParameterName}' of type '{opt.TypeName}' was null.\"); ");
                    }
                    else if (opt.TypeName == "string" && opt.DefaultValue is not null)
                    {
                        bindingCode.Append($"var {varName} = {extraction} ?? {opt.DefaultValue}; ");
                    }
                    else if (opt.TypeName == "string")
                    {
                        // Required reference types - throw descriptive exception if null instead of using null-forgiving
                        bindingCode.Append($"var {varName} = {extraction} ?? throw new global::System.InvalidOperationException(\"Required parameter '{opt.ParameterName}' of type '{opt.TypeName}' was null.\"); ");
                    }
                    else
                    {
                        // Required value types - extraction already has default value fallback
                        bindingCode.Append($"var {varName} = {extraction}; ");
                    }

                    paramList.Add(varName);
                }
            }

            string paramStr = string.Join(", ", paramList);
            string call = $"{bindingCode}{CallCode(c, $"{targetExpr}.{c.MethodName}({paramStr})")}";

            // Only use 'static' keyword if the method is actually static
            string lambdaModifier = c.IsStatic ? "static " : "";
            return $"new global::SimpleDiscordNet.Commands.CommandHandler(HasContext: {(c.HasContext ? "true" : "false")}, AutoDefer: {(c.AutoDefer ? "true" : "false")}, DeferEphemeral: {(c.DeferEphemeral ? "true" : "false")}, Invoke: {lambdaModifier}async (ctx, ct) => {{ {call} }}{ChecksField(c)})";
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("namespace SimpleDiscordNet.Generated");
        sb.AppendLine("{");

        // Emit optional singletons for instance types
        foreach (var typeName in candidates.Where(static c => !c.IsStatic).Select(static c => c.ContainingType).Distinct())
        {
            // Check if ANY instance method from this type has a default constructor
            var instanceMethod = candidates.FirstOrDefault(c => c.ContainingType == typeName && !c.IsStatic);
            if (instanceMethod == null || !instanceMethod.HasDefaultCtor) continue; // will diagnose below

            if (instanceMethod.InjectedTypes.Count > 0)
            {
                // Services aren't known until the bot is built, so this waits for the first use
                var arguments = instanceMethod.InjectedTypes.Select(static t => $"global::SimpleDiscordNet.Commands.HandlerServices.Get<{t}>()");
                sb.AppendLine($"    internal static class __InstHolder_{SanitizeId(typeName)} {{ private static {typeName}? s_value; internal static {typeName} Value => s_value ??= new {typeName}({string.Join(", ", arguments)}); }}");
                continue;
            }

            sb.AppendLine($"    internal static class __InstHolder_{SanitizeId(typeName)} {{ internal static readonly {typeName} Value = new {typeName}(); }}");
        }

        foreach (var form in forms)
        {
            EmitModalForm(sb, form);
        }

        // Generated manifest/provider
        sb.AppendLine("    internal sealed class __GeneratedManifest : global::SimpleDiscordNet.Commands.IGeneratedManifest");
        sb.AppendLine("    {");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        private static global::SimpleDiscordNet.Models.InteractionOption? FindOption(global::System.Collections.Generic.IReadOnlyList<global::SimpleDiscordNet.Models.InteractionOption> options, string name)");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (var opt in options)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (opt.Name == name) return opt;");
        sb.AppendLine("            }");
        sb.AppendLine("            return null;");
        sb.AppendLine("        }");
        sb.AppendLine();
        // Ungrouped
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::SimpleDiscordNet.Commands.CommandHandler> Ungrouped { get; } = new global::System.Collections.Generic.Dictionary<string, global::SimpleDiscordNet.Commands.CommandHandler>(System.StringComparer.Ordinal)");
        sb.AppendLine("        {");
        foreach (var u in ungrouped)
        {
            var instExpr = u.IsStatic ? null : (u.HasDefaultCtor ? $"__InstHolder_{SanitizeId(u.ContainingType)}.Value" : null);
            if (!u.IsStatic && instExpr is null) continue;
            sb.AppendLine($"            [\"{u.SlashName}\"] = {BuildHandlerFactory(u)},");
        }
        // Context menu handlers (same pattern as ungrouped commands)
        foreach (var cm in contextMenus)
        {
            var instExpr = cm.IsStatic ? null : (cm.HasDefaultCtor ? $"__InstHolder_{SanitizeId(cm.ContainingType)}.Value" : null);
            if (!cm.IsStatic && instExpr is null) continue;
            sb.AppendLine($"            [\"{cm.SlashName}\"] = {BuildHandlerFactory(cm)},");
        }
        if (helpName is not null)
        {
            sb.AppendLine($"            [{Lit(helpName)}] = new global::SimpleDiscordNet.Commands.CommandHandler(HasContext: true, AutoDefer: true, DeferEphemeral: true, Invoke: static async (ctx, ct) => await global::SimpleDiscordNet.Commands.HelpFormatter.SendAsync(ctx, s_help, {Lit(helpTitle)}, ct).ConfigureAwait(false)),");
        }
        sb.AppendLine("        };");

        // Grouped
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::System.Collections.Generic.IReadOnlyDictionary<string, global::SimpleDiscordNet.Commands.CommandHandler>> Grouped { get; } = new global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.IReadOnlyDictionary<string, global::SimpleDiscordNet.Commands.CommandHandler>>(System.StringComparer.Ordinal)");
        sb.AppendLine("        {");
        foreach (var g in grouped)
        {
            sb.AppendLine($"            [\"{g.Key}\"] = new global::System.Collections.Generic.Dictionary<string, global::SimpleDiscordNet.Commands.CommandHandler>(System.StringComparer.Ordinal)");
            sb.AppendLine("            {");
            foreach (var sc in g.Value)
            {
                var instExpr = sc.IsStatic ? null : (sc.HasDefaultCtor ? $"__InstHolder_{SanitizeId(sc.ContainingType)}.Value" : null);
                if (!sc.IsStatic && instExpr is null) continue;
                sb.AppendLine($"                [\"{sc.SlashName}\"] = {BuildHandlerFactory(sc)},");
            }
            sb.AppendLine("            },");
        }
        sb.AppendLine("        };");

        // SubGrouped
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::System.Collections.Generic.IReadOnlyDictionary<string, global::System.Collections.Generic.IReadOnlyDictionary<string, global::SimpleDiscordNet.Commands.CommandHandler>>> SubGrouped { get; } = new global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.IReadOnlyDictionary<string, global::System.Collections.Generic.IReadOnlyDictionary<string, global::SimpleDiscordNet.Commands.CommandHandler>>>(System.StringComparer.Ordinal)");
        sb.AppendLine("        {");
        foreach (var g in subGrouped)
        {
            sb.AppendLine($"            [\"{g.Key}\"] = new global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.IReadOnlyDictionary<string, global::SimpleDiscordNet.Commands.CommandHandler>>(System.StringComparer.Ordinal)");
            sb.AppendLine("            {");
            foreach (var sg in g.Value)
            {
                sb.AppendLine($"                [\"{sg.Key}\"] = new global::System.Collections.Generic.Dictionary<string, global::SimpleDiscordNet.Commands.CommandHandler>(System.StringComparer.Ordinal)");
                sb.AppendLine("                {");
                foreach (var sc in sg.Value)
                {
                    var instExpr = sc.IsStatic ? null : (sc.HasDefaultCtor ? $"__InstHolder_{SanitizeId(sc.ContainingType)}.Value" : null);
                    if (!sc.IsStatic && instExpr is null) continue;
                    sb.AppendLine($"                    [\"{sc.SlashName}\"] = {BuildHandlerFactory(sc)},");
                }
                sb.AppendLine("                },");
            }
            sb.AppendLine("            },");
        }
        sb.AppendLine("        };");

        // Components
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::SimpleDiscordNet.Commands.ComponentHandler> Components { get; } = new global::System.Collections.Generic.List<global::SimpleDiscordNet.Commands.ComponentHandler>");
        sb.AppendLine("        {");
        foreach (var c in components)
        {
            var instExpr = c.IsStatic ? null : (c.HasDefaultCtor ? $"__InstHolder_{SanitizeId(c.ContainingType)}.Value" : null);
            if (!c.IsStatic && instExpr is null) continue;
            var handlerArgs = new List<string>();
            if (c.HasContext) handlerArgs.Add("ctx");
            if (c.ModalFormType is not null) handlerArgs.Add($"{ModalHelperName(c.ModalFormType)}.Bind(ctx)");
            handlerArgs.AddRange(c.TemplateArguments);
            string handlerTarget = c.IsStatic ? c.ContainingType : $"__InstHolder_{SanitizeId(c.ContainingType)}.Value";
            string handlerModifier = c.IsStatic ? "static " : string.Empty;
            string handlerCall = $"{handlerTarget}.{c.MethodName}({string.Join(", ", handlerArgs)})";
            string invoker = $"{handlerModifier}async (ctx, ct) => {{ {CallCode(c, handlerCall)} }}";
            string templateField = c.IsTemplate ? $", Template: new global::SimpleDiscordNet.Commands.ComponentIdTemplate({Lit(c.ComponentId)})" : string.Empty;
            sb.AppendLine($"            new global::SimpleDiscordNet.Commands.ComponentHandler({Lit(c.ComponentId)}, {c.ComponentPrefix.ToString().ToLowerInvariant()}, {c.HasContext.ToString().ToLowerInvariant()}, {c.AutoDefer.ToString().ToLowerInvariant()}, {c.DeferEphemeral.ToString().ToLowerInvariant()}, {invoker}{ChecksField(c)}{templateField}),");
        }
        sb.AppendLine("        };");

        // Autocomplete handlers
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::SimpleDiscordNet.Commands.AutocompleteHandler> AutocompleteHandlers { get; } = new global::System.Collections.Generic.Dictionary<string, global::SimpleDiscordNet.Commands.AutocompleteHandler>(System.StringComparer.Ordinal)");
        sb.AppendLine("        {");
        foreach (var a in autocompletes)
        {
            string key = !string.IsNullOrWhiteSpace(a.SubGroupName)
                ? $"{a.AutocompleteCommandName}:{a.SubGroupName}:{a.SlashName}:{a.AutocompleteOptionName}"
                : !string.IsNullOrWhiteSpace(a.GroupName)
                    ? $"{a.AutocompleteCommandName}:{a.SlashName}:{a.AutocompleteOptionName}"
                    : $"{a.AutocompleteCommandName}:{a.AutocompleteOptionName}";
            string autocompleteTarget = a.IsStatic ? a.ContainingType : $"__InstHolder_{SanitizeId(a.ContainingType)}.Value";
            string autocompleteCall = $"{autocompleteTarget}.{a.MethodName}({(a.HasContext ? "ctx" : string.Empty)})";
            string getChoices = a.ReturnKind == ReturnShape.AwaitableValue
                ? $"var choices = await {autocompleteCall}.ConfigureAwait(false);"
                : $"var choices = {autocompleteCall}; await global::System.Threading.Tasks.Task.CompletedTask;";
            string invoker = $"{(a.IsStatic ? "static " : string.Empty)}async (ctx, ct) => {{ {getChoices} if (choices is not null) return choices; return System.Array.Empty<global::SimpleDiscordNet.Models.CommandChoice>(); }}";
            sb.AppendLine($"            [\"{key}\"] = new global::SimpleDiscordNet.Commands.AutocompleteHandler(Invoke: {invoker}),");
        }
        sb.AppendLine("        };");

        // Definitions
        sb.AppendLine($"        public global::SimpleDiscordNet.Models.ApplicationCommandDefinition[] Definitions {{ get; }} = {defsBuilder};");

        // Help index (basic)
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyDictionary<string, string> HelpIndex => s_help;");
        sb.AppendLine("        private static readonly global::System.Collections.Generic.Dictionary<string, string> s_help = new global::System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal)");
        sb.AppendLine("        {");
        foreach (var u in ungrouped)
        {
            var desc = string.IsNullOrWhiteSpace(u.SlashDescription) ? "command" : u.SlashDescription!.Replace("\"", "\\\"");
            sb.AppendLine($"            [\"/{u.SlashName}\"] = \"{desc}\",");
        }
        foreach (var g in grouped)
        {
            foreach (var sc in g.Value)
            {
                var desc = string.IsNullOrWhiteSpace(sc.SlashDescription) ? "command" : sc.SlashDescription!.Replace("\"", "\\\"");
                sb.AppendLine($"            [\"/{g.Key} {sc.SlashName}\"] = \"{desc}\",");
            }
        }
        foreach (var g in subGrouped)
        {
            foreach (var sg in g.Value)
            {
                foreach (var sc in sg.Value)
                {
                    var desc = string.IsNullOrWhiteSpace(sc.SlashDescription) ? "command" : sc.SlashDescription!.Replace("\"", "\\\"");
                    sb.AppendLine($"            [\"/{g.Key} {sg.Key} {sc.SlashName}\"] = \"{desc}\",");
                }
            }
        }
        sb.AppendLine("        };");

        sb.AppendLine("    }");

        sb.AppendLine("    internal sealed class __GeneratedProvider : global::SimpleDiscordNet.Commands.IGeneratedManifestProvider");
        sb.AppendLine("    {");
        sb.AppendLine("        public global::SimpleDiscordNet.Commands.IGeneratedManifest CreateManifest() => new __GeneratedManifest();");
        sb.AppendLine("    }");

        sb.AppendLine("    internal static class __GeneratedModuleInitializer");
        sb.AppendLine("    {");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        public static void Init()");
        sb.AppendLine("        {");
        sb.AppendLine("            global::SimpleDiscordNet.Commands.GeneratedRegistry.Register(new __GeneratedProvider());");
        foreach (var form in forms)
        {
            sb.AppendLine($"            global::SimpleDiscordNet.Commands.ModalFormRegistry.Register(new global::SimpleDiscordNet.Commands.ModalFormDefinition(typeof({form.TypeName}), {Lit(form.Id)}, {Lit(form.Title)}, static initial => {ModalHelperName(form.TypeName)}.Build(({form.TypeName}?)initial)));");
        }
        foreach (var handler in eventHandlers.Where(static c => c.IsStatic || c.HasDefaultCtor))
        {
            foreach (string eventName in handler.EventNames)
            {
                sb.AppendLine($"            {EventHandlerCode(handler, eventName)}");
            }
        }
        if (errorHandlers.FirstOrDefault(static c => c.Problems.Count == 0 && (c.IsStatic || c.HasDefaultCtor)) is { } errorHandler)
        {
            sb.AppendLine($"            {ErrorHandlerCode(errorHandler)}");
        }
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        spc.AddSource($"{SanitizeId(assemblyName)}_SimpleDiscordNet_GeneratedManifest.g.cs", sb.ToString());

        // Report diagnostics for unsupported patterns
        foreach (var c in candidates.Where(static c => !c.IsStatic && !c.HasDefaultCtor))
        {
            ReportError(spc, c.Location, "SDN001", "Handler type can't be created",
                "Type '{0}' needs a public constructor whose parameters are services, or the method '{1}' must be static.",
                c.ContainingType, c.MethodName);
        }
    }

    private static string FormatDefaultValue(object? value)
    {
        if (value is null) return "null";
        return value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString() ?? "null";
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "cmd";

        var s = name.Trim().ToLowerInvariant().Replace(' ', '-');
        var sb = new StringBuilder(s.Length);

        foreach (char ch in s)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-' || ch == '_')
                sb.Append(ch);
        }

        if (sb.Length == 0) return "cmd";
        if (sb.Length > 32) sb.Length = 32;

        return sb.ToString();
    }

    private static string SanitizeId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "_";

        var sb = new StringBuilder(id.Length);
        foreach (char ch in id)
        {
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return sb.ToString();
    }

    private static string BuildOptionsArray(List<OptionParameter> options)
    {
        if (options.Count == 0) return "null";

        var sb = new StringBuilder();
        sb.Append("new global::SimpleDiscordNet.Models.ApplicationCommandDefinition[] { ");
        foreach (var opt in options)
        {
            // Discord option types: 3=STRING, 4=INTEGER, 5=BOOLEAN, 6=USER, 7=CHANNEL, 8=ROLE, 9=MENTIONABLE, 10=NUMBER, 11=ATTACHMENT
            int discordType = opt.TypeName switch
            {
                "string" => 3,
                "ulong" => 3,
                "long" => 4,
                "int" => 4,
                "bool" => 5,
                "User" => 6,
                "Member" => 6,
                "Channel" => 7,
                "Role" => 8,
                "double" => 10,
                "float" => 10,
                "Attachment" => 11,
                _ => 3 // Enums are string options, with a choice for each value
            };

            string optName = NormalizeName(opt.OptionName);
            string desc = opt.Description.Replace("\"", "\\\"");
            string discordTypeLiteral = discordType.ToString(CultureInfo.InvariantCulture);

            sb.Append($"new global::SimpleDiscordNet.Models.ApplicationCommandDefinition {{ name = \"{optName}\", type = {discordTypeLiteral}, description = \"{desc}\", required = {opt.IsRequired.ToString().ToLowerInvariant()}");

            // Add string constraints
            if (opt.MinLength.HasValue)
                sb.Append($", min_length = {opt.MinLength.Value.ToString(CultureInfo.InvariantCulture)}");
            if (opt.MaxLength.HasValue)
                sb.Append($", max_length = {opt.MaxLength.Value.ToString(CultureInfo.InvariantCulture)}");

            // Add numeric constraints
            if (opt.MinValue.HasValue)
                sb.Append($", min_value = {opt.MinValue.Value.ToString(CultureInfo.InvariantCulture)}");
            if (opt.MaxValue.HasValue)
                sb.Append($", max_value = {opt.MaxValue.Value.ToString(CultureInfo.InvariantCulture)}");

            // Add channel types
            if (!string.IsNullOrWhiteSpace(opt.ChannelTypes))
            {
                var typesParts = opt.ChannelTypes!.Split(',');
                var channelTypesList = new List<string>(typesParts.Length);
                foreach (var part in typesParts)
                {
                    var trimmed = part.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        channelTypesList.Add(trimmed);
                }
                sb.Append($", channel_types = new int[] {{ {string.Join(", ", channelTypesList)} }}");
            }

            // Add choices (parse the format "Display:value,Display2:value2")
            if (!string.IsNullOrWhiteSpace(opt.Choices))
            {
                var choicesList = new List<string>();
                foreach (var choiceStr in opt.Choices!.Split(','))
                {
                    var parts = choiceStr.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        string displayName = parts[0].Trim().Replace("\"", "\\\"");
                        string value = parts[1].Trim();

                        // Determine if value is numeric or string based on option type
                        string valueExpr = (discordType == 4 || discordType == 10)
                            ? value // Numeric - use as-is
                            : $"\"{value.Replace("\"", "\\\"")}\""; // String - quote it

                        choicesList.Add($"global::SimpleDiscordNet.Models.CommandChoice.Create(\"{displayName}\", {valueExpr})");
                    }
                }
                if (choicesList.Count > 0)
                {
                    sb.Append($", choices = new global::SimpleDiscordNet.Models.CommandChoice[] {{ {string.Join(", ", choicesList)} }}");
                }
            }

            // Add autocomplete flag
            if (opt.Autocomplete)
                sb.Append($", autocomplete = true");

            sb.Append(BuildLocalizationFields(opt.Localizations, normalizeNames: true, includeDescriptions: true));

            sb.Append(" }, ");
        }
        sb.Append("}");
        return sb.ToString();
    }

    private sealed class Candidate
    {
        public string Namespace { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string ContainingType { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public bool IsStatic { get; set; }
        public bool HasDefaultCtor { get; set; }
        public bool HasContext { get; set; }
        public bool IsSlash { get; set; }
        public bool IsAutocomplete { get; set; }
        public int? ContextMenuType { get; set; }
        public string? SlashName { get; set; }
        public string? SlashDescription { get; set; }
        public string? GroupName { get; set; }
        public string? GroupDescription { get; set; }
        public string? SubGroupName { get; set; }
        public string? SubGroupDescription { get; set; }
        public bool AutoDefer { get; set; }
        public bool DeferEphemeral { get; set; }
        public ulong? RequiredPermissions { get; set; }
        public bool IsComponent { get; set; }
        public string? ComponentId { get; set; }
        public bool ComponentPrefix { get; set; }
        public string? AutocompleteCommandName { get; set; }
        public string? AutocompleteOptionName { get; set; }
        public List<OptionParameter> Options { get; set; } = new List<OptionParameter>();
        public List<string> UnsupportedParameters { get; set; } = new List<string>();
        public ReturnShape ReturnKind { get; set; }
        public string? ReplyKind { get; set; }
        public Location Location { get; set; } = Location.None;
        public List<string> Checks { get; set; } = new List<string>();
        public List<string> InjectedTypes { get; set; } = new List<string>();
        public List<string> EventNames { get; set; } = new List<string>();
        public bool EventTakesData { get; set; }
        public bool IsErrorHandler { get; set; }
        public bool IsTemplate { get; set; }
        public List<string> TemplateArguments { get; set; } = new List<string>();
        public List<(string Id, string Title, string Message)> Problems { get; set; } = new List<(string Id, string Title, string Message)>();
        public bool IsModalHandler { get; set; }
        public string? ModalFormType { get; set; }
        public string? Contexts { get; set; }
        public string? IntegrationTypes { get; set; }
        public List<LocalizedText> Localizations { get; set; } = new List<LocalizedText>();
        public List<LocalizedText> GroupLocalizations { get; set; } = new List<LocalizedText>();
    }

    private enum ReturnShape
    {
        Void,
        Awaitable,
        AwaitableValue,
        Value
    }

    private sealed class LocalizedText
    {
        public string Locale { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    private sealed class ModalFormCandidate
    {
        public string TypeName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public Location Location { get; set; } = Location.None;
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool HasDefaultCtor { get; set; }
        public List<ModalField> Fields { get; set; } = new List<ModalField>();
        public List<string> Problems { get; set; } = new List<string>();
    }

    private sealed class ModalField
    {
        public string PropertyName { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ComponentCode { get; set; } = string.Empty;
        public string BindCode { get; set; } = string.Empty;
    }

    private sealed class OptionParameter
    {
        public string ParameterName { get; set; } = string.Empty;
        public string OptionName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty; // "string", "long", "int", "bool", "double", "float", "User", "Channel", "Role"
        public string ActualTypeName { get; set; } = string.Empty; // Actual C# type: "string", "long", "ulong", "int", "bool", etc.
        public bool IsNullable { get; set; }
        public bool IsRequired { get; set; }
        public string? DefaultValue { get; set; }

        // Constraints
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public string? ChannelTypes { get; set; }
        public string? Choices { get; set; }
        public bool Autocomplete { get; set; }
        public string? EnumExtraction { get; set; }
        public List<LocalizedText> Localizations { get; set; } = new List<LocalizedText>();
    }
}
