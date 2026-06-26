using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDefinery
{
    /// <summary>
    /// The Node class is a generic class for deserializing OpenDefinery responses when modifying content
    /// </summary>
    class Node
    {
        [JsonPropertyName("nid")]
        public Nid[] Nid { get; set; }

        [JsonPropertyName("uuid")]
        public Uuid[] Uuid { get; set; }

        [JsonPropertyName("vid")]
        public Nid[] Vid { get; set; }

        [JsonPropertyName("langcode")]
        public Langcode[] Langcode { get; set; }

        [JsonPropertyName("type")]
        public TypeElement[] Type { get; set; }

        [JsonPropertyName("revision_timestamp")]
        public RevisionTimestamp[] RevisionTimestamp { get; set; }

        [JsonPropertyName("status")]
        public DefaultLangcode[] Status { get; set; }

        [JsonPropertyName("title")]
        public Langcode[] Title { get; set; }

        [JsonPropertyName("created")]
        public Changed[] Created { get; set; }

        [JsonPropertyName("changed")]
        public Changed[] Changed { get; set; }

        [JsonPropertyName("promote")]
        public DefaultLangcode[] Promote { get; set; }

        [JsonPropertyName("sticky")]
        public DefaultLangcode[] Sticky { get; set; }

        [JsonPropertyName("default_langcode")]
        public DefaultLangcode[] DefaultLangcode { get; set; }

        [JsonPropertyName("revision_translation_affected")]
        public DefaultLangcode[] RevisionTranslationAffected { get; set; }

        [JsonPropertyName("path")]
        public Path[] Path { get; set; }

        [JsonPropertyName("field_guid")]
        public FieldGuid[] FieldGuid { get; set; }
    }

    public partial class FieldGuid
    {
        [JsonPropertyName("value")]
        public Guid Value { get; set; }
    }

    public partial class Changed
    {
        [JsonPropertyName("value")]
        public DateTimeOffset Value { get; set; }

        [JsonPropertyName("lang")]
        public string Lang { get; set; }

        [JsonPropertyName("format")]
        public string Format { get; set; }
    }

    public partial class DefaultLangcode
    {
        [JsonPropertyName("value")]
        public bool Value { get; set; }

        [JsonPropertyName("lang")]
        public string Lang { get; set; }
    }

    public partial class Uuid
    {
        [JsonPropertyName("value")]
        public Guid Value { get; set; }
    }

    public partial class Langcode
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("lang")]
        public string Lang { get; set; }
    }

    public partial class Nid
    {
        [JsonPropertyName("value")]
        public int Value { get; set; }
    }

    public partial class Path
    {
        [JsonPropertyName("alias")]
        public object Alias { get; set; }

        [JsonPropertyName("pid")]
        public object Pid { get; set; }

        [JsonPropertyName("langcode")]
        public string Langcode { get; set; }

        [JsonPropertyName("lang")]
        public string Lang { get; set; }
    }

    public partial class RevisionTimestamp
    {
        [JsonPropertyName("value")]
        public DateTimeOffset Value { get; set; }

        [JsonPropertyName("format")]
        public string Format { get; set; }
    }

    public partial class TypeElement
    {
        [JsonPropertyName("target_id")]
        public string TargetId { get; set; }
    }
}
