using System;
using DSLink.Nodes.Actions;
using Newtonsoft.Json.Linq;

namespace DSLink.Nodes
{
    /// <summary>
    /// Represents a remote Node that isn't on our Node tree.
    /// </summary>
    public class RemoteNode : Node
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="T:DSLink.Nodes.RemoteNode"/> class.
        /// </summary>
        /// <param name="name">Name</param>
        /// <param name="parent">Parent</param>
        /// <param name="path">Path of Node</param>
        public RemoteNode(string name, Node parent, string path) : base(name, parent, null)
        {
            Path = path;
        }

        /// <summary>
        /// <see cref="Node"/>
        /// </summary>
        public override NodeFactory CreateChild(string name)
        {
            throw new InvalidOperationException("Cannot create a remote node");
        }

        /// <summary>
        /// Set the value.
        /// </summary>
        /// <param name="value">Value</param>
        protected override void ValueSet(Value value)
        {
        }

        /// <summary>
        /// Updates the subscribers.
        /// </summary>
        internal override void UpdateSubscribers()
        {
        }

        /// <summary>
        /// Deserializes.
        /// </summary>
        /// <param name="serialized">Serialized</param>
        public void FromSerialized(JArray serialized)
        {
            foreach (var jToken in serialized)
            {
                var a = (JArray) jToken;
                var key = a[0].ToString();
                var value = a[1];
                if (key.StartsWith("$"))
                {
                    key = key.Substring(1);
                    if (key.Equals("params") && value.Type == JTokenType.Array)
                    {
                        var parameters = new JArray();
                        foreach (var parameter in value.Value<JArray>())
                        {
                            parameters.Add(parameter.ToObject<Parameter>());
                        }
                        SetConfig(key, new Value(parameters));
                    }
                    else if (key.Equals("columns") && value.Type == JTokenType.Array)
                    {
                        var columns = new JArray();
                        foreach (var column in value.Value<JArray>())
                        {
                            columns.Add(column.ToObject<Column>());
                        }
                        SetConfig(key, new Value(columns));
                    }
                    else
                    {
                        SetConfig(key, new Value(value.ToString()));
                    }
                }
                else if (key.StartsWith("@"))
                {
                    key = key.Substring(1);
                    SetAttribute(key, new Value(value.ToString()));
                }
                else
                {
                    var child = new RemoteNode(key, this, Path + "/" + key);
                    var jObject = value as JObject;
                    if (jObject != null)
                    {
                        foreach (var kp in jObject)
                        {
                            if (kp.Key.StartsWith("$"))
                            {
                                child.SetConfig(kp.Key.Substring(1), new Value(kp.Value.ToString()));
                            }
                            else if (kp.Key.StartsWith("@"))
                            {
                                child.SetAttribute(kp.Key.Substring(1), new Value(kp.Value.ToString()));
                            }
                        }
                    }
                    AddChild(child);
                }
            }
        }
    }
}
