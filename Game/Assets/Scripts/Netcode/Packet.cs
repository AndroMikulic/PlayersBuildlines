﻿using System;
using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class Packet {

	public string timestamp;
	public string type;
	public dynamic payload;
	public string json;

	public Packet () {

	}

	public Packet (String packetType, dynamic payload) {

		this.timestamp = DateTime.UtcNow.ToString ();
		this.type = packetType;
		this.payload = payload;

		dynamic data = new ExpandoObject ();
		data.type = this.type;
		data.payload = this.payload;
		this.json = JsonConvert.SerializeObject (data);
	}

	public static Packet Parse (string s) {
		dynamic obj;
		try {
			obj = JObject.Parse (s);
		} catch (Exception e) {
			Debug.Log (e);
			return null;
		}

		Packet packet = new Packet ();
		packet.type = obj.type;
		packet.payload = obj.payload;
		packet.timestamp = obj.timestamp;
		packet.json = s;

		return packet;
	}
}