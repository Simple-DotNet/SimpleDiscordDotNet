using System;
using System.Collections.Generic;
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
    private const string ComponentAttr = "SimpleDiscordNet.Commands.ComponentHandlerAttribute";
    private const string ButtonHandlerAttr = "SimpleDiscordNet.Commands.ButtonHandlerAttribute";
    private const string SelectMenuHandlerAttr = "SimpleDiscordNet.Commands.SelectMenuHandlerAttribute";
    private const string UserSelectHandlerAttr = "SimpleDiscordNet.Commands.UserSelectHandlerAttribute";
    private const string RoleSelectHandlerAttr = "SimpleDiscordNet.Commands.RoleSelectHandlerAttribute";
    private const string ChannelSelectHandlerAttr = "SimpleDiscordNet.Commands.ChannelSelectHandlerAttribute";
    private const string MentionableSelectHandlerAttr = "SimpleDiscordNet.Commands.MentionableSelectHandlerAttribute";
    private const string NoDeferAttr = "SimpleDiscordNet.Commands.NoDeferAttribute";
    private const string CommandOptionAttr = "SimpleDiscordNet.Commands.CommandOptionAttribute";
    private const string CommandChoiceAttr = "SimpleDiscordNet.Commands.CommandChoiceAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var methodsWithAttrs = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0,
                                  static (ctx, ct) => GetCandidate(ctx))
            .Where(static c => c is not null)!
            .Select(static (c, _) => c!);

        var compilationAndCandidates = context.CompilationProvider.Combine(methodsWithAttrs.Collect());

        context.RegisterSourceOutput(compilationAndCandidates, static (spc, tuple) =>
        {
            var (compilation, items) = tuple;
            Emit(spc, compilation.AssemblyName ?? "Assembly", items);
        });
    }

    private static Candidate? GetCandidate(GeneratorSyntaxContext ctx)
    {
        var methodSyntax = (MethodDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol ms)
            return null;

        bool isSlash = false;
        bool isComponent = false;
        string? slashName = null;
        string? slashDescription = null;
        bool autoDefer = true; // default: auto-defer enabled, use [NoDefer] to disable
        bool methodHasNoDefer = false; // track if method has explicit [NoDefer]
        string? componentId = null;
        bool componentPrefix = false;

        foreach (var ad in ms.GetAttributes())
        {
            var name = ad.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':');
            if (name == SlashAttr)
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
        }

        if (!isSlash && !isComponent) return null;

        // Group attribute on containing type
        string? groupName = null;
        string? groupDescription = null;
        if (ms.ContainingType is { } ct)
        {
            foreach (var ad in ct.GetAttributes())
            {
                var name = ad.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':');
                if (name == SlashGroupAttr)
                {
                    if (ad.ConstructorArguments.Length >= 1)
                        groupName = ad.ConstructorArguments[0].Value as string;
                    if (ad.ConstructorArguments.Length >= 2)
                        groupDescription = ad.ConstructorArguments[1].Value as string;
                    break;
                }
                // Check for [NoDefer] on the class level (only if method didn't have explicit [NoDefer])
                else if (name == NoDeferAttr && !methodHasNoDefer)
                {
                    autoDefer = false; // [NoDefer] disables auto-defer
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

        // Parse command option parameters (for slash commands only)
        var options = new List<OptionParameter>();
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
                                choiceList.Add($"{displayName}:{value}");
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

                // Map C# types to Discord option types - support primitives and entity types
                string discordType = typeName switch
                {
                    "string" => "string",
                    "long" => "long",
                    "ulong" => "long", // Discord uses long (i64) for all integers
                    "int" => "int",
                    "bool" => "bool",
                    "double" => "double",
                    "float" => "float",
                    "User" => "User",
                    "Channel" => "Channel",
                    "Role" => "Role",
                    _ => typeName.EndsWith(".User") ? "User" :
                         typeName.EndsWith(".Channel") ? "Channel" :
                         typeName.EndsWith(".Role") ? "Role" : "unknown"
                };

                if (discordType == "unknown") continue; // Skip unsupported types

                bool isRequired = explicitRequired ?? (!isNullable && !param.HasExplicitDefaultValue);
                string? defaultValue = param.HasExplicitDefaultValue ? (param.ExplicitDefaultValue?.ToString() ?? "null") : null;

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
                    Autocomplete = autocomplete
                });
            }
        }

        // Only parameterless instance or any static methods are supported
        bool isStatic = ms.IsStatic;
        bool hasDefaultCtor = false;
        if (!isStatic)
        {
            foreach (var c in ms.ContainingType.InstanceConstructors)
            {
                if (c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 0)
                {
                    hasDefaultCtor = true;
                    break;
                }
            }
        }

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
            SlashName = slashName,
            SlashDescription = slashDescription,
            GroupName = groupName,
            GroupDescription = groupDescription,
            AutoDefer = autoDefer,
            IsComponent = isComponent,
            ComponentId = componentId,
            ComponentPrefix = componentPrefix,
            Options = options
        };
    }

    private static string GetNamespace(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace;
        if (ns is null || ns.IsGlobalNamespace) return string.Empty;
        return ns.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimStart('g', 'l', 'o', 'b', 'a', 'l', ':');
    }

    private static void Emit(SourceProductionContext spc, string assemblyName, IReadOnlyList<Candidate> candidates)
    {
        var commands = candidates.Where(c => c.IsSlash && !string.IsNullOrWhiteSpace(c.SlashName)).ToList();
        var components = candidates.Where(c => c.IsComponent && !string.IsNullOrWhiteSpace(c.ComponentId)).ToList();

        // Organize slash commands
        var ungrouped = new List<Candidate>();
        var grouped = new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);

        foreach (var c in commands)
        {
            var normName = NormalizeName(c.SlashName!);
            c.SlashName = normName;
            if (!string.IsNullOrWhiteSpace(c.GroupName))
            {
                var grp = NormalizeName(c.GroupName!);
                c.GroupName = grp;
                if (!grouped.TryGetValue(grp, out var list))
                {
                    list = new List<Candidate>();
                    grouped[grp] = list;
                }
                list.Add(c);
            }
            else
            {
                ungrouped.Add(c);
            }
        }

        // Build definitions array source
        var defsBuilder = new StringBuilder();
        defsBuilder.AppendLine("new global::SimpleDiscordNet.Models.ApplicationCommandDefinition[] {");
        foreach (var g in grouped)
        {
            defsBuilder.AppendLine("    new global::SimpleDiscordNet.Models.ApplicationCommandDefinition { ");
            defsBuilder.AppendLine($"        name = \"{g.Key}\", ");
            defsBuilder.AppendLine("        type = 1,");
            var gdesc = g.Value.Count > 0 && !string.IsNullOrWhiteSpace(g.Value[0]?.GroupDescription) ? g.Value[0].GroupDescription!.Replace("\"", "\\\"") : "group";
            defsBuilder.AppendLine($"        description = \"{gdesc}\",");
            defsBuilder.AppendLine("        options = new global::SimpleDiscordNet.Models.ApplicationCommandDefinition[] {");

            var seen = new HashSet<(string, string)>();
            foreach (var sc in g.Value)
            {
                var key = (sc.SlashName!, sc.SlashDescription ?? "command");
                if (!seen.Add(key)) continue;

                var desc = string.IsNullOrWhiteSpace(sc.SlashDescription) ? "command" : sc.SlashDescription!.Replace("\"", "\\\"");
                var optsArray = BuildOptionsArray(sc.Options);
                defsBuilder.AppendLine($"            new global::SimpleDiscordNet.Models.ApplicationCommandDefinition {{ name = \"{sc.SlashName}\", type = 1, description = \"{desc}\", options = {optsArray} }},");
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
            defsBuilder.AppendLine($"    new global::SimpleDiscordNet.Models.ApplicationCommandDefinition {{ name = \"{u.SlashName}\", type = 1, description = \"{desc}\", options = {optsArray} }},");
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

                    // Generate extraction code based on type and nullability
                    // For entity types (User/Channel/Role), we need to retrieve from cache using the ID
                    string extraction;

                    if (opt.IsNullable)
                    {
                        // Nullable types - return nullable values
                        extraction = opt.ActualTypeName switch
                        {
                            "string" => $"FindOption(_opts, \"{normalizedName}\")?.String",
                            "long" => $"FindOption(_opts, \"{normalizedName}\")?.Integer",
                            "ulong" => $"FindOption(_opts, \"{normalizedName}\")?.Integer is long _ulongVal_{opt.ParameterName} ? (ulong)_ulongVal_{opt.ParameterName} : (ulong?)null",
                            "int" => $"FindOption(_opts, \"{normalizedName}\")?.Integer is long _intVal_{opt.ParameterName} ? (int)_intVal_{opt.ParameterName} : (int?)null",
                            "bool" => $"FindOption(_opts, \"{normalizedName}\")?.Boolean",
                            "double" => $"FindOption(_opts, \"{normalizedName}\")?.Integer is long _doubleVal_{opt.ParameterName} ? (double)_doubleVal_{opt.ParameterName} : (double?)null",
                            "float" => $"FindOption(_opts, \"{normalizedName}\")?.Integer is long _floatVal_{opt.ParameterName} ? (float)_floatVal_{opt.ParameterName} : (float?)null",
                            "User" => $"FindOption(_opts, \"{normalizedName}\")?.String is string _userId_{opt.ParameterName} && ctx.Event?.GuildId != null ? global::SimpleDiscordNet.DiscordBot.Cache.GetMember(_userId_{opt.ParameterName}, ctx.Event.GuildId)?.User : null",
                            "Channel" => $"FindOption(_opts, \"{normalizedName}\")?.String is string _channelId_{opt.ParameterName} && ctx.Event?.GuildId != null ? global::SimpleDiscordNet.DiscordBot.Cache.GetChannel(_channelId_{opt.ParameterName}, ctx.Event.GuildId) : null",
                            "Role" => $"FindOption(_opts, \"{normalizedName}\")?.String is string _roleId_{opt.ParameterName} && ctx.Event?.GuildId != null ? global::SimpleDiscordNet.DiscordBot.Cache.GetRole(_roleId_{opt.ParameterName}, ctx.Event.GuildId) : null",
                            _ => "null"
                        };
                    }
                    else
                    {
                        // Non-nullable types - provide defaults for value types
                        extraction = opt.ActualTypeName switch
                        {
                            "string" => $"FindOption(_opts, \"{normalizedName}\")?.String",
                            "long" => $"FindOption(_opts, \"{normalizedName}\")?.Integer ?? 0L",
                            "ulong" => $"(ulong)(FindOption(_opts, \"{normalizedName}\")?.Integer ?? 0L)",
                            "int" => $"(int)(FindOption(_opts, \"{normalizedName}\")?.Integer ?? 0L)",
                            "bool" => $"FindOption(_opts, \"{normalizedName}\")?.Boolean ?? false",
                            "double" => $"FindOption(_opts, \"{normalizedName}\")?.Integer is long _doubleVal_{opt.ParameterName} ? (double)_doubleVal_{opt.ParameterName} : 0.0",
                            "float" => $"FindOption(_opts, \"{normalizedName}\")?.Integer is long _floatVal_{opt.ParameterName} ? (float)_floatVal_{opt.ParameterName} : 0f",
                            "User" => $"FindOption(_opts, \"{normalizedName}\")?.String is string _userId_{opt.ParameterName} && ctx.Event?.GuildId != null ? global::SimpleDiscordNet.DiscordBot.Cache.GetMember(_userId_{opt.ParameterName}, ctx.Event.GuildId)?.User : null",
                            "Channel" => $"FindOption(_opts, \"{normalizedName}\")?.String is string _channelId_{opt.ParameterName} && ctx.Event?.GuildId != null ? global::SimpleDiscordNet.DiscordBot.Cache.GetChannel(_channelId_{opt.ParameterName}, ctx.Event.GuildId) : null",
                            "Role" => $"FindOption(_opts, \"{normalizedName}\")?.String is string _roleId_{opt.ParameterName} && ctx.Event?.GuildId != null ? global::SimpleDiscordNet.DiscordBot.Cache.GetRole(_roleId_{opt.ParameterName}, ctx.Event.GuildId) : null",
                            _ => "null"
                        };
                    }

                    // Add parameter binding with proper nullability handling
                    if (opt.IsNullable)
                    {
                        // Nullable types - extraction already returns nullable, no operator needed
                        bindingCode.Append($"var {varName} = {extraction}; ");
                    }
                    else if (opt.TypeName == "string" || opt.TypeName == "User" || opt.TypeName == "Channel" || opt.TypeName == "Role")
                    {
                        // Required reference types - add null-forgiving operator (Discord should guarantee these exist for required params)
                        bindingCode.Append($"var {varName} = {extraction}!; ");
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
            string call = $"{bindingCode}var _res = {targetExpr}.{c.MethodName}({paramStr}); if (_res is System.Threading.Tasks.Task t) await t.ConfigureAwait(false);";

            // Only use 'static' keyword if the method is actually static
            string lambdaModifier = c.IsStatic ? "static " : "";
            return $"new global::SimpleDiscordNet.Commands.CommandHandler(HasContext: {(c.HasContext ? "true" : "false")}, AutoDefer: {(c.AutoDefer ? "true" : "false")}, Invoke: {lambdaModifier}async (ctx, ct) => {{ {call} }})";
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
        foreach (var typeName in commands.Where(c => !c.IsStatic).Select(c => c.ContainingType).Concat(components.Where(c => !c.IsStatic).Select(c => c.ContainingType)).Distinct())
        {
            // Check if ANY instance method from this type has a default constructor
            var instanceMethod = candidates.FirstOrDefault(c => c.ContainingType == typeName && !c.IsStatic);
            if (instanceMethod == null || !instanceMethod.HasDefaultCtor) continue; // will diagnose below
            sb.AppendLine($"    internal static class __InstHolder_{SanitizeId(typeName)} {{ internal static readonly {typeName} Value = new {typeName}(); }}");
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

        // Components
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::SimpleDiscordNet.Commands.ComponentHandler> Components { get; } = new global::System.Collections.Generic.List<global::SimpleDiscordNet.Commands.ComponentHandler>");
        sb.AppendLine("        {");
        foreach (var c in components)
        {
            var instExpr = c.IsStatic ? null : (c.HasDefaultCtor ? $"__InstHolder_{SanitizeId(c.ContainingType)}.Value" : null);
            if (!c.IsStatic && instExpr is null) continue;
            string invoker = c.HasContext ? (c.IsStatic ? $"static async (ctx, ct) => {{ var _r = {c.ContainingType}.{c.MethodName}(ctx); if (_r is System.Threading.Tasks.Task t) await t.ConfigureAwait(false); }}" : $"async (ctx, ct) => {{ var _r = __InstHolder_{SanitizeId(c.ContainingType)}.Value.{c.MethodName}(ctx); if (_r is System.Threading.Tasks.Task t) await t.ConfigureAwait(false); }}")
                                          : (c.IsStatic ? $"static async (ctx, ct) => {{ var _r = {c.ContainingType}.{c.MethodName}(); if (_r is System.Threading.Tasks.Task t) await t.ConfigureAwait(false); }}" : $"async (ctx, ct) => {{ var _r = __InstHolder_{SanitizeId(c.ContainingType)}.Value.{c.MethodName}(); if (_r is System.Threading.Tasks.Task t) await t.ConfigureAwait(false); }}");
            sb.AppendLine($"            new global::SimpleDiscordNet.Commands.ComponentHandler(\"{c.ComponentId}\", {c.ComponentPrefix.ToString().ToLowerInvariant()}, {c.HasContext.ToString().ToLowerInvariant()}, {c.AutoDefer.ToString().ToLowerInvariant()}, {invoker}),");
        }
        sb.AppendLine("        };");

        // Definitions
        sb.AppendLine($"        public global::SimpleDiscordNet.Models.ApplicationCommandDefinition[] Definitions {{ get; }} = {defsBuilder};");

        // Help index (basic)
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyDictionary<string, string> HelpIndex { get; } = new global::System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal)");
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
        sb.AppendLine("        };");

        sb.AppendLine("    }");

        sb.AppendLine("    internal sealed class __GeneratedProvider : global::SimpleDiscordNet.Commands.IGeneratedManifestProvider");
        sb.AppendLine("    {");
        sb.AppendLine("        public global::SimpleDiscordNet.Commands.IGeneratedManifest CreateManifest() => new __GeneratedManifest();");
        sb.AppendLine("    }");

        sb.AppendLine("    internal static class __GeneratedModuleInitializer");
        sb.AppendLine("    {");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        public static void Init() => global::SimpleDiscordNet.Commands.GeneratedRegistry.Register(new __GeneratedProvider());");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        spc.AddSource($"{SanitizeId(assemblyName)}_SimpleDiscordNet_GeneratedManifest.g.cs", sb.ToString());

        // Report diagnostics for unsupported patterns
        foreach (var c in commands.Concat(components))
        {
            if (!c.IsStatic && !c.HasDefaultCtor)
            {
                spc.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                    id: "SDN001",
                    title: "Handler type must have a public parameterless constructor or the method must be static",
                    messageFormat: "Type '{0}' must have a public parameterless constructor or the method '{1}' must be static for source-generated handlers.",
                    category: "SimpleDiscordNet",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                    Location.None,
                    c.ContainingType, c.MethodName));
            }
        }
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
                "long" => 4,
                "int" => 4,
                "bool" => 5,
                "User" => 6,
                "Channel" => 7,
                "Role" => 8,
                "double" => 10,
                "float" => 10,
                _ => 3
            };

            string optName = NormalizeName(opt.OptionName);
            string desc = opt.Description.Replace("\"", "\\\"");

            sb.Append($"new global::SimpleDiscordNet.Models.ApplicationCommandDefinition {{ name = \"{optName}\", type = {discordType}, description = \"{desc}\", required = {opt.IsRequired.ToString().ToLowerInvariant()}");

            // Add string constraints
            if (opt.MinLength.HasValue)
                sb.Append($", min_length = {opt.MinLength.Value}");
            if (opt.MaxLength.HasValue)
                sb.Append($", max_length = {opt.MaxLength.Value}");

            // Add numeric constraints
            if (opt.MinValue.HasValue)
                sb.Append($", min_value = {opt.MinValue.Value}");
            if (opt.MaxValue.HasValue)
                sb.Append($", max_value = {opt.MaxValue.Value}");

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

                        choicesList.Add($"new global::SimpleDiscordNet.Models.CommandChoice {{ name = \"{displayName}\", value = {valueExpr} }}");
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
        public string? SlashName { get; set; }
        public string? SlashDescription { get; set; }
        public string? GroupName { get; set; }
        public string? GroupDescription { get; set; }
        public bool AutoDefer { get; set; }
        public bool IsComponent { get; set; }
        public string? ComponentId { get; set; }
        public bool ComponentPrefix { get; set; }
        public List<OptionParameter> Options { get; set; } = new List<OptionParameter>();
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
    }
}
