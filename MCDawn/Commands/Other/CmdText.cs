﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MCDawn
{
	public class CmdText : Command
	{
		public override string name { get { return "text"; } }
		public override string[] aliases { get { return new string[] { "" }; } }
		public override string type { get { return "other"; } }
		public override bool museumUsable { get { return true; } }
		public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }
		public CmdText() { }
		
		private string toLowerDictionary = "abcdefghijklmnopqrstuvwxyz1234567890-_";

		public override void Use(Player p, string message)
		{
			if (!Directory.Exists("extra/text/")) Directory.CreateDirectory("extra/text");
			if (message.IndexOf(' ') == -1) { Help(p); return; }

			try
			{
				if (message.Split(' ')[0].ToLower() == "delete")
				{
					if (!IsValid(message.Split(' ')[0])) {
						Player.SendMessage(p, "File name is invalid. The file name can only contain the following characters:");
						Player.SendMessage(p, this.toLowerDictionary);
						return;
					}
					
					if (File.Exists("extra/text/" + message.Split(' ')[1] + ".txt"))
					{
						File.Delete("extra/text/" + message.Split(' ')[1] + ".txt");
						Player.SendMessage(p, "Deleted file");
					}
					else
					{
						Player.SendMessage(p, "Could not find file specified");
					}
				}
				else
				{
					if (!IsValid(message.Split(' ')[0])) {
						Player.SendMessage(p, "File name is invalid. Please only use the following characters in your file name:");
						Player.SendMessage(p, this.toLowerDictionary);
						return;
					}
					
					bool again = false;
					string fileName = "extra/text/" + message.Split(' ')[0] + ".txt";
					string group = Group.findPerm(LevelPermission.Guest).name;
					if (Group.Find(message.Split(' ')[1]) != null)
					{
						group = Group.Find(message.Split(' ')[1]).name;
						again = true;
					}
					message = message.Substring(message.IndexOf(' ') + 1);
					if (again)
						message = message.Substring(message.IndexOf(' ') + 1);
					string contents = message;
					if (contents == "") { Help(p); return; }
					if (!File.Exists(fileName))
						contents = "#" + group + System.Environment.NewLine + contents;
					else
						contents = " " + contents;
					File.AppendAllText(fileName, contents);
					Player.SendMessage(p, "Added text");
				}
			} catch { Help(p); }
		}
		
		public override void Help(Player p)
		{
			Player.SendMessage(p, "/text [file] [rank] [message] - Makes a /view-able text");
			Player.SendMessage(p, "The [rank] entered is the minimum rank to view the file");
			Player.SendMessage(p, "The [message] is entered into the text file");
			Player.SendMessage(p, "If the file already exists, text will be added to the end");
		}
		
		private bool IsValid(string st) {
			bool found = false;
			foreach (char c in st.ToLower()) {
				foreach (char ch in this.toLowerDictionary)
					if (ch == c) found = true;
				if (!found) return false;
				else found = false;
			}
			return true;
		}
	}
}