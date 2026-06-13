using System.Text.Json.Nodes;
using AuraTxt.Core.Models;

namespace AuraTxt.Core.Util;

public static class JsonPathSetter
{
    /// Merges <paramref name="value"/> into <paramref name="root"/> at the dot-path <paramref name="path"/>.
    /// Intermediate nodes are created as needed. Leaf uses shallow merge if existing node is JsonObject.
    public static void SetPath(JsonObject root, string path, JsonObject value)
    {
        var segments = path.Split('.');
        var node = root;

        foreach (var seg in segments[..^1])
        {
            if (!node.ContainsKey(seg))
            {
                node[seg] = new JsonObject();
            }
            else if (node[seg] is not JsonObject)
            {
                throw new ProfileApplicationException(
                    $"Path segment '{seg}' exists in JSON body but is not an object");
            }
            node = (JsonObject)node[seg]!;
        }

        var last = segments[^1];
        if (node[last] is JsonObject existing)
        {
            foreach (var kvp in value)
                existing[kvp.Key] = kvp.Value?.DeepClone();
        }
        else
        {
            node[last] = value.DeepClone();
        }
    }
}
