﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using NSUci;
using RapLog;

namespace NSProgram
{
	class Program
	{
		public static bool isLog = false;
		public static CBook book = new CBook();
		public static CRapLog log = new CRapLog();

		static void Main(string[] args)
		{
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
			bool isInfo = false;
			/// <summary>
			/// Book can write new moves.
			/// </summary>
			bool isW = false;
			/// <summary>
			/// Limit ply to read.
			/// </summary>
			int bookLimitR = 32;
			/// <summary>
			/// Limit ply to write.
			/// </summary>
			int bookLimitW = 32;
			CUci uci = new CUci();
			CChessExt chess = CBook.chess;
			string ax = "-bf";
			List<string> listBf = new List<string>();
			List<string> listEf = new List<string>();
			List<string> listEa = new List<string>();
			for (int n = 0; n < args.Length; n++)
			{
				string ac = args[n];
				switch (ac)
				{
					case "-bf":
					case "-ef":
					case "-ea":
					case "-lr"://limit read in half moves
					case "-lw"://limit write in half moves
						ax = ac;
						break;
					case "-w":
						ax = ac;
						isW = true;
						break;
					case "-log"://add log
						ax = ac;
						isLog = true;
						break;
					case "-info":
						ax = ac;
						isInfo = true;
						break;
					default:
						switch (ax)
						{
							case "-bf":
								listBf.Add(ac);
								break;
							case "-ef":
								listEf.Add(ac);
								break;
							case "-ea":
								listEa.Add(ac);
								break;
							case "-lr":
								bookLimitR = int.TryParse(ac, out int lr) ? lr : 0;
								break;
							case "-lw":
								bookLimitW = int.TryParse(ac, out int lw) ? lw : 0;
								break;
							case "-w":
								ac = ac.Replace("K", "000").Replace("M", "000000");
								book.maxRecords = int.TryParse(ac, out int m) ? m : 0;
								break;
						}
						break;
				}
			}
			string bookFile = String.Join(" ", listBf);
			string engineFile = String.Join(" ", listEf);
			string engineArguments = String.Join(" ", listEa);

			string ext = Path.GetExtension(bookFile);
			if (String.IsNullOrEmpty(ext))
				bookFile = $"{bookFile}{CBook.defExt}";
			bool bookLoaded = book.LoadFromFile(bookFile);
			if (bookLoaded)
			{
				if (book.recList.Count > 0)
					Console.WriteLine($"info string book on {book.recList.Count:N0} moves 128 bpm");
				if (isW)
					Console.WriteLine($"info string write on");
			}
			else
				isW = false;
			Process engineProcess = null;
			if (File.Exists(engineFile))
			{
				engineProcess = new Process();
				engineProcess.StartInfo.FileName = engineFile;
				engineProcess.StartInfo.WorkingDirectory = Path.GetDirectoryName(engineFile);
				engineProcess.StartInfo.UseShellExecute = false;
				engineProcess.StartInfo.RedirectStandardInput = true;
				engineProcess.StartInfo.Arguments = engineArguments;
				engineProcess.Start();
				Console.WriteLine($"info string engine on");
			}
			else
			{
				if (engineFile != String.Empty)
					Console.WriteLine($"info string missing engine  [{engineFile}]");
				engineFile = String.Empty;
			}
			if (isInfo)
				book.InfoMoves();
			do
			{
				string msg = Console.ReadLine().Trim();
				if ((msg == "help") || (msg == "book"))
				{
					Console.WriteLine("book load [filename].[bin|pgn|uci] - clear and add moves from file");
					Console.WriteLine("book save [filename].[bin] - save book to the file");
					Console.WriteLine("book addfile [filename].[bin|pgn|uci] - add moves to the book");
					Console.WriteLine("book adduci [uci] - add moves in uci format to the book");
					Console.WriteLine("book clear - clear all moves from the book");
					continue;
				}
				uci.SetMsg(msg);
				if (uci.command == "book")
				{
					if (uci.tokens.Length > 1)
						switch (uci.tokens[1])
						{
							case "addfile":
								string fn = uci.GetValue("addfile");
								if (File.Exists(fn))
								{
									book.AddFile(fn);
									book.ShowMoves(true);
								}
								else Console.WriteLine("File not found");
								break;
							case "adduci":
								string movesUci = uci.GetValue("adduci");
								book.AddUci(movesUci);
								break;
							case "clear":
								book.Clear();
								book.ShowMoves();
								break;
							case "load":
								if (!book.LoadFromFile(uci.GetValue("load")))
									Console.WriteLine("File not found");
								else
									book.ShowMoves(true);
								break;
							case "save":
								book.SaveToFile(uci.GetValue("save"));
								break;
							case "moves":
								book.InfoMoves(uci.GetValue("moves"));
								break;
							case "getoption":
								Console.WriteLine($"option name Book file type string default book{CBook.defExt}");
								Console.WriteLine($"option name Write type check default false");
								Console.WriteLine($"option name Log type check default false");
								Console.WriteLine($"option name Limit read moves type spin default {bookLimitR} min 0 max 100");
								Console.WriteLine($"option name Limit write moves type spin default {bookLimitW} min 0 max 100");
								Console.WriteLine("optionok");
								break;
							case "setoption":
								switch (uci.GetValue("name", "value").ToLower())
								{
									case "book file":
										bookLoaded = book.LoadFromFile(uci.GetValue("value"));
										break;
									case "write":
										isW = uci.GetValue("value") == "true";
										break;
									case "log":
										isLog = uci.GetValue("value") == "true";
										break;
									case "limit read":
										bookLimitR = uci.GetInt("value");
										break;
									case "limit write":
										bookLimitW = uci.GetInt("value");
										break;
								}
								break;
							default:
								Console.WriteLine($"Unknown command [{uci.tokens[1]}]");
								break;
						}
					continue;
				}
				if ((uci.command != "go") && !String.IsNullOrEmpty(engineFile))
					engineProcess.StandardInput.WriteLine(msg);
				switch (uci.command)
				{
					case "position":
						List<string> movesUci = new List<string>();
						string fen = uci.GetValue("fen", "moves");
						chess.SetFen(fen);
						int i = uci.GetIndex("moves", uci.tokens.Length);
						for (int n = i; n < uci.tokens.Length; n++)
						{
							string m = uci.tokens[n];
							movesUci.Add(m);
							chess.MakeMove(m, out _);
						}
						if (isW && bookLoaded && String.IsNullOrEmpty(fen) && chess.Is2ToEnd(out string myMove, out string enMove))
						{
							movesUci.Add(myMove);
							movesUci.Add(enMove);
							book.AddUci(movesUci, bookLimitW, false);
							book.SaveToFile();
						}
						break;
					case "go":
						string move = String.Empty;
						if ((bookLimitR == 0) || (bookLimitR > chess.g_moveNumber))
						{
							move = book.GetMove();
							if (!chess.IsValidMove(move, out _))
								move = String.Empty;
						}
						if (!String.IsNullOrEmpty(move))
							Console.WriteLine($"bestmove {move}");
						else if (engineProcess == null)
							Console.WriteLine("enginemove");
						else
							engineProcess.StandardInput.WriteLine(msg);
						break;
				}
			} while (uci.command != "quit");
		}

	}
}
