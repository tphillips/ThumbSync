using System;
using FlickrNet;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ThumbSync
{
	class MainClass
	{
		
		static bool smallPhotos = false;
		static bool largeSize = false;
		static bool thumbnailSize = false;
		static int maxPerPerson = 20;
		static string userIds = "";
		static string OutDir = "";
		static string token = "";
		static bool overwrite = false;
		static int page = 1;
		static int pageSize = 10;
		static int x = 1;
		static int collected = 0;
		static int throttle = 0;
		static bool checkForWhiteFiles = false;
		
		static bool ParseArgs(string[] args)
		{
			try
			{
				for (int x = 0; x < args.Length; x++)
				{
					if (args[x] == "-u") { userIds = args[x+1]; }
					if (args[x] == "-c") { maxPerPerson = int.Parse (args[x+1]); }
					if (args[x] == "-th") { throttle = int.Parse (args[x+1]); }
					if (args[x] == "-s") { smallPhotos = true; }
					if (args[x] == "-l") { largeSize = true; }
					if (args[x] == "-W") { checkForWhiteFiles = true; }
					if (args[x] == "-o") { OutDir = args[x+1]; }
					if (args[x] == "-t") { thumbnailSize = true; }
					if (args[x] == "-T") { token = args[x+1]; }
					if (args[x] == "-O") { overwrite = true; }
					if (args[x] == "-p") { page = int.Parse (args[x+1]); collected = (page -1) * pageSize; x = collected; }
				}
			}
			catch
			{
				return false;
			}
			return !String.IsNullOrEmpty(userIds) && !String.IsNullOrEmpty(OutDir);
		}
		
		public static void Main (string[] args)
		{
			Console.WriteLine("ThumbSync - Flickr Sync by Tristan Phillips   v0.4");
			if (!ParseArgs (args))
			{
				Console.WriteLine ("usage: ThumbSync.exe -u <userIDs , seperated> -o <out directory>\n" + 
				                   "Optionals:\n" +
				                   "-c <count, default 20> \n" +
				                   "-s get small photos (default medium) \n" +
				                   "-t get thumbnails (default medium) \n" +
				                   "-l get large photos (default medium) \n" +
				                   "-T <pre authenticated token> \n" +
				                   "-O overwrite existing local copy \n" +
				                   "-p <start at page> \n" +
				                   "-th <throttle millisecs> \n" +
				                   "-W use white average file validation");
				return;
			}
			string[] userIDs = userIds.Split (',');
			FlickrNet.Flickr f = new FlickrNet.Flickr("5f9dbe6d11086346eacc6d9b9d81a5f5", "826ddba13f621f18");
			if (token == "")
			{
				string frob = f.AuthGetFrob ();
				string url = f.AuthCalcUrl(frob, AuthLevel.Read);
				Console.WriteLine ("Go here & authenticate: " + url);
				Console.WriteLine ("When you are done, press return . . .  I'll be waiting . . .");
				try
				{
					/*
					System.Diagnostics.Process p = new System.Diagnostics.Process();
					p.StartInfo.FileName="c:\\program files\\internet explorer\\iExplore.exe";
					p.StartInfo.Arguments = url;
					p.Start();
					System.Diagnostics.Process p2 = new System.Diagnostics.Process();
					p2.StartInfo.FileName="/Applications/Safari.app/Contents/MacOS/Safari";
					p2.StartInfo.Arguments = "\"" + url + "\"";
					p2.Start();
					*/
				}
				catch{}
				Console.ReadLine ();
				FlickrNet.Auth auth =  f.AuthGetToken (frob);
				Console.WriteLine ("Token (you can re-use this with -T): " + auth.Token);
				token = auth.Token;
			}
			f.AuthToken = token;
			foreach(string userId in userIDs)
			{
				
				Person per = f.PeopleGetInfo (userId);
				string personName = per.UserName;
				Console.WriteLine ("Processing " + maxPerPerson + " from " + per.UserName + "(" + per.RealName + ")");
				while (collected < maxPerPerson)
				{
					PhotoCollection res = f.PeopleGetPhotos(userId, page, pageSize);
					collected += res.Count;
					if (res.Page == res.Pages) { collected = maxPerPerson; }
					foreach(Photo p in res)
					{
						bool processed = false;
						int tries = 0;
						int startSecsWait = 120;
						int maxTries = 15;
						while(!processed && tries < maxTries)
						{
							try
							{
								tries ++;
								Console.Write (". ");
								if (x%10==0) { Console.Write(x + " "); }
								PhotoInfo info = f.PhotosGetInfo (p.PhotoId);
								string tag = info.Tags.Count > 0 ? info.Tags[0].Raw : "NotTagged";
								if (!System.IO.Directory.Exists(OutDir + System.IO.Path.DirectorySeparatorChar + personName + "-" + tag))
								{
									System.IO.Directory.CreateDirectory (OutDir + System.IO.Path.DirectorySeparatorChar + personName + "-" +  tag);
								}
								string url = smallPhotos ? p.SmallUrl : p.MediumUrl;
								url = largeSize ? p.LargeUrl : url;
								url = thumbnailSize ? p.ThumbnailUrl: url;
								string[] pNames = url.Split ('/');
								string pName = pNames[pNames.Length-1];
								string fileName = OutDir + System.IO.Path.DirectorySeparatorChar + personName + "-" + tag + System.IO.Path.DirectorySeparatorChar + pName; 
								if (!System.IO.File.Exists (fileName) || overwrite)
								{
									new System.Net.WebClient().DownloadFile (url, fileName);
									if (checkForWhiteFiles)
									{
										WhiteCheck(fileName);
									}
								}
								processed = true;
								x++;
							}
							catch(Exception e)
							{
								
								int wait = startSecsWait * tries;
								Console.WriteLine (String.Format("There was a problem processing page {0} (x={1}, page={2}, pageSize={3}, collected={4})", page, x, page, pageSize, collected));
								Console.WriteLine (e.Message);
								if (tries < maxTries){ Console.WriteLine ("Will retry in " + wait / 60  + " mins . . ."); }
								System.Threading.Thread.Sleep (wait * 1000);
							}
						}
						if (throttle > 0) { System.Threading.Thread.Sleep(throttle); }
					}
					page ++;
				}
			}
			Console.WriteLine ("Done");
		}

		static void WhiteCheck(string fileName)
		{
			using (Bitmap b = new System.Drawing.Bitmap (fileName)) {
				int boundX = b.Width;
				int boundY = b.Height;
				long pixels = 0;
				long tot = 0;
				long avg = 0;
				for (int px = 0; px < boundX; px++) {
					for (int py = 0; py < boundY; py++) {
						Color c = b.GetPixel (px, py);
						tot += (c.R + c.G + c.B);
						pixels++;
					}
				}
				avg = tot / pixels;
				if (avg < 45 || avg > 720)//#f0f0f0
				{
					throw new Exception ("White average too great, blank image?");
				}
			}
		}
	}
}
