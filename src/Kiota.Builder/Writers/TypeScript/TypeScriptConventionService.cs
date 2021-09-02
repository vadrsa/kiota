using System;
using System.Linq;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.TypeScript {
    public class TypeScriptConventionService : CommonLanguageConventionService
    {
        public TypeScriptConventionService(LanguageWriter languageWriter)
        {
            writer = languageWriter;
        }
        private readonly LanguageWriter writer;
        public override string StreamTypeName => "ReadableStream";

        public override string VoidTypeName => throw new System.NotImplementedException();

        public override string DocCommentPrefix => " * ";

        public override string PathSegmentPropertyName => "pathSegment";

        public override string CurrentPathPropertyName => "currentPath";

        public override string HttpCorePropertyName => "httpCore";

        public override string ParseNodeInterfaceName => "ParseNode";

        public override string RawUrlPropertyName => "isRawUrl";

        internal string DocCommentStart = "/**";
        internal string DocCommentEnd = " */";
        internal void AddRequestBuilderBody(bool addCurrentPath, string returnType, LanguageWriter writer, string suffix = default, string additionalPathParameters = default) {
            var currentPath = addCurrentPath ? $"this.{CurrentPathPropertyName} + " : string.Empty;
            writer.WriteLines($"return new {returnType}({currentPath}this.{PathSegmentPropertyName}{suffix}, this.{HttpCorePropertyName}{additionalPathParameters}, false);");
        }

        public override string GetAccessModifier(AccessModifier access)
        {
            return access switch {
                AccessModifier.Public => "public",
                AccessModifier.Protected => "protected",
                _ => "private",
            };
        }

        public override string GetParameterSignature(CodeParameter parameter)
        {
            var defaultValueSuffiix = string.IsNullOrEmpty(parameter.DefaultValue) ? string.Empty : $" = {parameter.DefaultValue}";
            return $"{parameter.Name}{(parameter.Optional && parameter.Type.IsNullable ? "?" : string.Empty)}: {GetTypeString(parameter.Type)}{(parameter.Type.IsNullable ? " | undefined": string.Empty)}{defaultValueSuffiix}";
        }

        public override string GetTypeString(CodeTypeBase code)
        {
            var collectionSuffix = code.CollectionKind == CodeType.CodeTypeCollectionKind.None ? string.Empty : "[]";
            if(code is CodeUnionType currentUnion && currentUnion.Types.Any())
                return currentUnion.Types.Select(x => GetTypeString(x)).Aggregate((x, y) => $"{x} | {y}") + collectionSuffix;
            else if(code is CodeType currentType) {
                var typeName = TranslateType(currentType);
                if (code.ActionOf)
                    return WriteInlineDeclaration(currentType);
                else
                    return $"{typeName}{collectionSuffix}";
            }
            else throw new InvalidOperationException($"type of type {code.GetType()} is unknown");
        }
        private string WriteInlineDeclaration(CodeType currentType) {
            writer.IncreaseIndent(4);
            var childElements = (currentType?.TypeDefinition as CodeClass)
                                        ?.GetChildElements(true)
                                        ?.OfType<CodeProperty>()
                                        ?.OrderBy(x => x.Name)
                                        ?.Select(x => $"{x.Name}?: {GetTypeString(x.Type)}");
            var innerDeclaration = childElements?.Any() ?? false ? 
                                            LanguageWriter.NewLine +
                                            writer.GetIndent() +
                                            childElements
                                            .Aggregate((x, y) => $"{x};{LanguageWriter.NewLine}{writer.GetIndent()}{y}")
                                            .Replace(';', ',') +
                                            LanguageWriter.NewLine +
                                            writer.GetIndent()
                                        : string.Empty;
            writer.DecreaseIndent();
            if(string.IsNullOrEmpty(innerDeclaration))
                return "object";
            else
                return $"{{{innerDeclaration}}}";
        }

        public override string TranslateType(CodeType type)
        {
            return (type.Name) switch  {//TODO we're probably missing a bunch of type mappings
                "integer" => "number",
                "double" => "number",
                "string" or "object" or "boolean" or "void" => type.Name, // little casing hack
                _ => type.Name.ToFirstCharacterUpperCase() ?? "object",
            };
        }
        public bool IsPrimitiveType(string typeName) {
            return typeName switch {
                "number" or "string" or "byte[]" or "boolean" or "void" => true,
                _ => false,
            };
        }
        internal static string RemoveInvalidDescriptionCharacters(string originalDescription) => originalDescription?.Replace("\\", "/");
        public override void WriteShortDescription(string description, LanguageWriter writer)
        {
            if(!string.IsNullOrEmpty(description))
                writer.WriteLine($"{DocCommentStart} {RemoveInvalidDescriptionCharacters(description)} {DocCommentEnd}");
        }
    }
}
