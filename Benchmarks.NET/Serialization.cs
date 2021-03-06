﻿using System;
using System.Collections.Generic;
using DSLink.Connection.Serializer;
using Newtonsoft.Json.Linq;

namespace Benchmarks.NET
{
    public class Serialization
    {
        private JsonSerializer _json;
        private MsgPackSerializer _msgpack;
        private JObject _serializeObject;

        public Serialization()
        {
            _json = new JsonSerializer();
            _msgpack = new MsgPackSerializer();
            var random = new Random();
            var byteBuffer = new byte[50000000];
            random.NextBytes(byteBuffer);
            _serializeObject = new JObject
            {
                new JProperty("responses", new JArray
                {
                    new JObject
                    {
                        new JProperty("updates", new JArray
                        {
                            new JArray
                            {
                                byteBuffer
                            }
                        })
                    }
                })
            };
        }

        public void JsonSerialize()
        {
            _json.Serialize(_serializeObject);
        }

        public void MsgPackSerialize()
        {
            _msgpack.Serialize(_serializeObject);
        }
    }
}
