﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using NSUci;

namespace NSProgram
{
	class Program
	{
		static void Main(string[] args)
		{
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
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
			CUci Uci = new CUci();
			CPolyglot Book = new CPolyglot();
			CChessExt chess = CPolyglot.Chess;
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
								Book.maxRecords = int.TryParse(ac, out int m) ? m : 0;
								break;
						}
						break;
				}
			}
			string bookFile = String.Join(" ", listBf);
			string engineFile = String.Join(" ", listEf);
			string arguments = String.Join(" ", listEa);

			string ext = Path.GetExtension(bookFile);
			if (String.IsNullOrEmpty(ext))
				bookFile = $"{bookFile}{CPolyglot.defExt}";
			bool fileLoaded = Book.LoadFromFile(bookFile);
			if (fileLoaded)
				Console.WriteLine($"info string book on");

			Process engineProcess = null;
			if (File.Exists(engineFile))
			{
				engineProcess = new Process();
				engineProcess.StartInfo.FileName = engineFile;
				engineProcess.StartInfo.WorkingDirectory = Path.GetDirectoryName(engineFile);
				engineProcess.StartInfo.UseShellExecute = false;
				engineProcess.StartInfo.RedirectStandardInput = true;
				engineProcess.StartInfo.Arguments = arguments;
				engineProcess.Start();
				Console.WriteLine($"info string engine on");
			}
			else
			{
				if (engineFile != String.Empty)
					Console.WriteLine($"info string missing engine  [{engineFile}]");
				engineFile = String.Empty;
			}

			Console.WriteLine($"info string book {Book.recList.Count:N0} moves");
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
				Uci.SetMsg(msg);
				if (Uci.command == "book")
				{
					if (Uci.tokens.Length > 1)
						switch (Uci.tokens[1])
						{
							case "addfile":
								string fn = Uci.GetValue(2, 0);
								if (File.Exists(fn))
								{
									Book.AddFile(fn);
									Book.ShowMoves(true);
								}
								else Console.WriteLine("File not found");
								break;
							case "adduci":
								string movesUci = Uci.GetValue(2, 0);
								Book.AddUci(movesUci);
								break;
							case "clear":
								Book.Clear();
								break;
							case "load":
								if (!Book.LoadFromFile(Uci.GetValue(2, 0)))
									Console.WriteLine("File not found");
								else
									Book.ShowMoves(true);
								break;
							case "save":
								Book.SaveToFile(Uci.GetValue(2, 0));
								break;
							default:
								Console.WriteLine($"Unknown command [{Uci.tokens[1]}]");
								break;
						}
					continue;
				}
				if ((Uci.command != "go") && !String.IsNullOrEmpty(engineFile))
					engineProcess.StandardInput.WriteLine(msg);
				switch (Uci.command)
				{
					case "position":
						List<string> movesUci = new List<string>();
						string fen = Uci.GetValue("fen", "moves");
						chess.SetFen(fen);
						int lo = Uci.GetIndex("moves");
						if (lo++ > 0)
						{
							int hi = Uci.GetIndex("fen", Uci.tokens.Length);
							if (hi < lo)
								hi = Uci.tokens.Length;
							for (int n = lo; n < hi; n++)
							{
								string m = Uci.tokens[n];
								movesUci.Add(m);
								chess.MakeMove(m, out _);
							}
						}
						if (isW && fileLoaded && String.IsNullOrEmpty(fen) && chess.Is2ToEnd(out string myMove, out string enMove))
						{
							movesUci.Add(myMove);
							movesUci.Add(enMove);
							Book.AddUci(movesUci, bookLimitW, false);
							Book.SaveToFile();
						}
						break;
					case "go":
						string move = String.Empty;
						if ((bookLimitR == 0) || (bookLimitR > chess.g_moveNumber))
						{
							move = Book.GetMove();
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
			} while (Uci.command != "quit");

		}
	}
}
