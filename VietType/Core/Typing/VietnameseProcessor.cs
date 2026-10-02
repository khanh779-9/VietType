namespace VietType.Core.Typing;

public class VietnameseProcessor
{
	public struct Chu
	{
		public bool D9;

		public bool UODau;

		public bool UOA;

		public string amDau;

		public string nguyenAm;

		public string amCuoi;

		public int vitriDau;

		public int vitriThanhPhan;

		public int trungdau;

		public Dau dau;

		public char moc;

		public void Reset()
		{
			vitriThanhPhan = 0;
			D9 = (UODau = (UOA = false));
			amDau = (nguyenAm = (amCuoi = ""));
			dau = Dau.khongDau;
			moc = '-';
			vitriDau = (trungdau = -1);
		}
	}

	public enum Dau
	{
		khongDau,
		sac,
		huyen,
		nga,
		hoi,
		nang
	}

	private string[] van = new string[318]
	{
		"a", "^", "a", "-", "a", "(", "ac", "^", "ac", "-",
		"ac", "(", "ach", "-", "ai", "-", "ak", "(", "am", "^",
		"am", "-", "am", "(", "an", "^", "an", "-", "an", "(",
		"ang", "^", "ang", "-", "ang", "(", "anh", "-", "ao", "-",
		"ap", "^", "ap", "-", "ap", "(", "at", "^", "at", "-",
		"at", "(", "au", "^", "au", "-", "ay", "^", "ay", "-",
		"e", "^", "e", "-", "ec", "-", "ech", "^", "em", "^",
		"em", "-", "en", "^", "en", "-", "eng", "-", "eng", "^",
		"enh", "^", "eo", "-", "ep", "^", "ep", "-", "et", "^",
		"et", "-", "eu", "^", "i", "-", "ia", "-", "ich", "-",
		"iec", "^", "iem", "^", "ien", "^", "ieng", "^", "iep", "^",
		"iet", "^", "ieu", "^", "im", "-", "in", "-", "inh", "-",
		"ip", "-", "it", "-", "iu", "-", "o", "^", "o", "-",
		"o", "*", "oa", "-", "oac", "-", "oac", "(", "oach", "-",
		"oai", "-", "oan", "-", "oan", "(", "oang", "-", "oang", "(",
		"oanh", "-", "oap", "-", "oat", "-", "oat", "-", "oat", "(",
		"oay", "-", "oc", "^", "oc", "-", "oe", "-", "oeo", "-",
		"oet", "-", "oi", "^", "oi", "-", "oi", "*", "om", "^",
		"om", "-", "om", "*", "on", "^", "on", "-", "on", "*",
		"ong", "^", "ong", "-", "ooc", "-", "op", "^", "op", "-",
		"op", "*", "ot", "^", "ot", "-", "ot", "*", "u", "-",
		"u", "*", "ua", "-", "ua", "*", "uan", "^", "uang", "^",
		"uat", "^", "uay", "^", "uc", "-", "uc", "*", "ue", "^",
		"uech", "^", "uenh", "^", "ui", "-", "ui", "*", "um", "-",
		"um", "*", "un", "-", "ung", "-", "ung", "*", "uo", "*",
		"uoc", "^", "uoc", "*", "uoi", "^", "uoi", "*", "uom", "^",
		"uom", "*", "uon", "^", "uon", "*", "uong", "^", "uong", "*",
		"uop", "*", "uot", "^", "uot", "*", "uou", "*", "up", "-",
		"ut", "-", "ut", "*", "uu", "*", "uy", "-", "uych", "-",
		"uyen", "^", "uyet", "^", "uynh", "-", "uyp", "-", "uyu", "-",
		"uyt", "-", "y", "-", "ych", "-", "yem", "^", "yen", "^",
		"yet", "^", "yeu", "^", "ynh", "-", "yt", "-"
	};

	private string[] amDau = new string[26]
	{
		"r", "d", "gi", "v", "ch", "tr", "s", "x", "l", "n",
		"qu", "b", "`c", "/k", "`g", "/gh", "h", "kh", "m", "`ng",
		"/ngh", "nh", "p", "ph", "t", "th"
	};

	private string[] phuAmCuoi = new string[8] { "c", "ch", "m", "n", "ng", "nh", "p", "t" };

	private string[] sacNang = new string[4] { "c", "ch", "p", "t" };

	public string[] bangMa;

	public char[] kieuGo;

	private int i;

	private int kq;

	public int ktChinhTa;

	private TypingMode viTriDauMoc;

	public Chu chu;

	private string s = string.Empty;

	private string value = string.Empty;

	private string stringChinhTa = string.Empty;

	public bool boDauKieuMoi;

	public bool vanChiIE;

	private char mocCu;

	public VietnameseProcessor(string[] ma, char[] go)
	{
		bangMa = ma;
		kieuGo = go;
		chu.Reset();
	}

	public bool KiemTraKetThucTu(char c)
	{
		if ((c < 'A' || (c > 'Z' && c < 'a') || c > 'z') && TimKiemKieuGo(c) == TypingMode.Null)
		{
			if (c >= '0' && c <= '9')
			{
				return false;
			}
			return true;
		}
		return false;
	}

	public bool KiemTraDauMuD()
	{
		if (chu.dau == Dau.khongDau && chu.moc == '-' && !chu.D9)
		{
			return false;
		}
		return true;
	}

	public void Reset()
	{
		chu.Reset();
	}

	public string Convert(string nguon)
	{
		s = (value = "");
		chu.Reset();
		for (i = 0; i < nguon.Length; i++)
		{
			if (chu.vitriThanhPhan == 0)
			{
				viTriDauMoc = TimKiemKieuGo(nguon[i]);
				if (viTriDauMoc == TypingMode.DThanhD)
				{
					if (chu.amDau.ToLower() == "d")
					{
						chu.D9 = true;
						chu.vitriThanhPhan = 1;
					}
					else
					{
						chu.amDau += nguon[i];
					}
				}
				else if (viTriDauMoc == TypingMode.UThanh7OThanh7AThanh8)
				{
					chu.vitriThanhPhan = 1;
					chu.moc = '*';
					if (nguon[i] == '}' || nguon[i] == 'W' || (nguon[i] >= 'A' && nguon[i] <= 'Z'))
					{
						chu.nguyenAm = "U";
					}
					else
					{
						chu.nguyenAm = "u";
					}
					chu.UOA = true;
				}
				else if (viTriDauMoc == TypingMode.UThanh7OThanh7)
				{
					chu.vitriThanhPhan = 1;
					chu.moc = '*';
					if (nguon[i] == '{' || (nguon[i] >= 'A' && nguon[i] <= 'Z'))
					{
						chu.nguyenAm = "O";
					}
					else
					{
						chu.nguyenAm = "o";
					}
					chu.UOA = true;
				}
				else if (KiemTraNguyenAm(nguon[i]))
				{
					chu.vitriThanhPhan = 1;
					chu.nguyenAm = nguon[i].ToString();
				}
				else
				{
					chu.amDau += nguon[i];
				}
			}
			else if (chu.vitriThanhPhan == 1)
			{
				viTriDauMoc = TimKiemKieuGo(nguon[i]);
				if ((chu.nguyenAm.Length > 0 && KiemTraNguyenAm(nguon[i])) || ((viTriDauMoc == TypingMode.UThanh7OThanh7AThanh8 || viTriDauMoc == TypingMode.UThanh7OThanh7) && chu.nguyenAm.Length == 1))
				{
					if ((chu.amDau == "g" || chu.amDau == "G") && (chu.nguyenAm[0] == 'i' || chu.nguyenAm[0] == 'I'))
					{
						chu.amDau += chu.nguyenAm[0];
						chu.nguyenAm = chu.nguyenAm.Substring(1);
					}
					else if ((chu.amDau == "q" || chu.amDau == "Q") && (chu.nguyenAm[0] == 'u' || chu.nguyenAm[0] == 'U'))
					{
						chu.amDau += chu.nguyenAm[0];
						chu.nguyenAm = chu.nguyenAm.Substring(1);
					}
				}
				if (viTriDauMoc == TypingMode.UThanh7OThanh7AThanh8 && chu.nguyenAm.Length == 0)
				{
					if (nguon[i] == '}' || nguon[i] == 'W' || (nguon[i] >= 'A' && nguon[i] <= 'Z'))
					{
						chu.nguyenAm = "U";
					}
					else
					{
						chu.nguyenAm = "u";
					}
					chu.UOA = true;
				}
				else if (viTriDauMoc == TypingMode.UThanh7OThanh7 && chu.nguyenAm.Length == 0)
				{
					if (nguon[i] == '{' || (nguon[i] >= 'A' && nguon[i] <= 'Z'))
					{
						chu.nguyenAm = "O";
					}
					else
					{
						chu.nguyenAm = "o";
					}
					chu.UOA = true;
				}
				if (viTriDauMoc >= TypingMode.khongDau && (TimKiemAmDau() || ktChinhTa != 2))
				{
					if (viTriDauMoc == TypingMode.DThanhD)
					{
						if (chu.amDau == "d" || chu.amDau == "D")
						{
							if (chu.D9)
							{
								chu.D9 = false;
								chu.trungdau = i;
								chu.vitriThanhPhan = 2;
								chu.amCuoi = nguon[i].ToString();
							}
							else
							{
								chu.D9 = true;
							}
						}
						else
						{
							chu.vitriThanhPhan = 2;
							chu.amCuoi += nguon[i];
						}
					}
					else if (!ThemVaoChu(viTriDauMoc, i, nguon[i]))
					{
						if (KiemTraNguyenAm(nguon[i]))
						{
							chu.nguyenAm += nguon[i];
						}
						else
						{
							chu.vitriThanhPhan = 2;
							chu.amCuoi += nguon[i];
						}
					}
				}
				else if (KiemTraNguyenAm(nguon[i]))
				{
					chu.nguyenAm += nguon[i];
				}
				else
				{
					chu.vitriThanhPhan = 2;
					chu.amCuoi += nguon[i];
				}
				if (chu.trungdau >= 0)
				{
					chu.vitriThanhPhan = 2;
					chu.amCuoi = nguon[i].ToString();
				}
			}
			else if (chu.vitriThanhPhan == 2)
			{
				viTriDauMoc = TimKiemKieuGo(nguon[i]);
				if (viTriDauMoc >= TypingMode.khongDau && (TimKiemAmDau() || ktChinhTa == 0) && TimKiemPhuAmCuoi() && chu.trungdau == -1)
				{
					if (viTriDauMoc == TypingMode.DThanhD)
					{
						if (chu.amDau == "d" || chu.amDau == "D")
						{
							if (chu.D9)
							{
								chu.D9 = false;
								chu.trungdau = i;
								chu.vitriThanhPhan = 2;
								chu.amCuoi += nguon[i];
							}
							else
							{
								chu.D9 = true;
							}
						}
						else
						{
							chu.amCuoi += nguon[i];
						}
					}
					else if (!ThemVaoChu(viTriDauMoc, i, nguon[i]) || chu.trungdau >= 0)
					{
						chu.amCuoi += nguon[i];
					}
				}
				else
				{
					chu.amCuoi += nguon[i];
				}
				if (ktChinhTa != 0 && chu.trungdau < 0 && chu.amCuoi.Length > 0 && KiemTraNguyenAm(chu.amCuoi[chu.amCuoi.Length - 1]))
				{
					return nguon;
				}
			}
		}
		if (chu.D9)
		{
			if (chu.amDau == "D")
			{
				s = bangMa[145];
			}
			else
			{
				if (!(chu.amDau == "d"))
				{
					return nguon;
				}
				s = bangMa[72];
			}
		}
		else
		{
			s = chu.amDau;
		}
		string? convertedValue = ChuyenSangVietnameseProcessor();
		if (convertedValue != null)
		{
			value = convertedValue;
			return s + convertedValue + chu.amCuoi;
		}
		if (chu.nguyenAm == "")
		{
			return s + chu.amCuoi;
		}
		return nguon;
	}

	public string ConvertNguoc()
	{
		if (chu.vitriDau >= chu.nguyenAm.Length || chu.vitriDau < 0 || chu.dau == Dau.khongDau)
		{
			chu.vitriDau = -1;
			chu.dau = Dau.khongDau;
			value = chu.amDau + chu.nguyenAm;
		}
		else
		{
			value = chu.amDau + chu.nguyenAm + kieuGo[(int)chu.dau];
		}
		if (chu.D9 && (chu.amDau == "d" || chu.amDau == "D"))
		{
			value += kieuGo[14];
		}
		if (chu.moc == '-')
		{
			return value + chu.amCuoi;
		}
		if (chu.moc == '(')
		{
			if (chu.nguyenAm.IndexOf('a') >= 0 || chu.nguyenAm.IndexOf('A') >= 0)
			{
				return value + kieuGo[13] + chu.amCuoi;
			}
			return value + chu.amCuoi;
		}
		if (chu.moc == '*')
		{
			char[] tam = "uUoO".ToCharArray();
			if (chu.nguyenAm.IndexOfAny(tam) >= 0)
			{
				if (kieuGo[12] != ' ')
				{
					return value + kieuGo[12] + chu.amCuoi;
				}
				return value + kieuGo[10] + chu.amCuoi;
			}
			return value + chu.amCuoi;
		}
		char[] tam2 = "aAoOeE".ToCharArray();
		if (chu.nguyenAm.IndexOfAny(tam2) < 0)
		{
			return value + chu.amCuoi;
		}
		if (kieuGo[6] != ' ')
		{
			return value + kieuGo[6] + chu.amCuoi;
		}
		if (chu.nguyenAm.IndexOf('a') >= 0)
		{
			return value + kieuGo[7] + chu.amCuoi;
		}
		if (chu.nguyenAm.IndexOf('e') >= 0)
		{
			return value + kieuGo[8] + chu.amCuoi;
		}
		return value + kieuGo[9] + chu.amCuoi;
	}

	private bool ThemVaoChu(TypingMode dnkg, int trungDau, char kyTuGo = '\0')
	{
		if (dnkg >= TypingMode.khongDau && dnkg <= TypingMode.nang)
		{
			KiemTraViTriDau();
			if (chu.vitriDau == -1 || chu.nguyenAm.Length == 0)
			{
				return false;
			}
			if (chu.dau == (Dau)dnkg)
			{
				if (chu.dau == Dau.khongDau)
				{
					return false;
				}
				chu.trungdau = trungDau;
				chu.dau = Dau.khongDau;
				chu.vitriThanhPhan = 2;
			}
			if (chu.trungdau == -1)
			{
				chu.dau = (Dau)dnkg;
			}
			return true;
		}
		mocCu = chu.moc;
		switch (dnkg)
		{
		case TypingMode.DauMuChungAOE:
		{
			char[] c = new char[6] { 'a', 'A', 'o', 'O', 'e', 'E' };
			if (chu.nguyenAm.IndexOfAny(c) < 0)
			{
				return false;
			}
			chu.moc = '^';
			break;
		}
		case TypingMode.AThanh6:
		{
			char[] c2 = new char[2] { 'a', 'A' };
			char[] c8 = new char[4] { 'o', 'O', 'e', 'E' };
			if (chu.nguyenAm.IndexOfAny(c2) < 0 || chu.nguyenAm.IndexOfAny(c8) >= 0)
			{
				return false;
			}
			chu.moc = '^';
			break;
		}
		case TypingMode.EThanh6:
		{
			char[] c3 = new char[2] { 'E', 'e' };
			char[] c9 = new char[4] { 'a', 'A', 'o', 'O' };
			if (chu.nguyenAm.IndexOfAny(c3) < 0 || chu.nguyenAm.IndexOfAny(c9) >= 0)
			{
				return false;
			}
			chu.moc = '^';
			break;
		}
		case TypingMode.OThanh6:
		{
			char[] c4 = new char[2] { 'O', 'o' };
			char[] c10 = new char[4] { 'a', 'A', 'e', 'E' };
			if (chu.nguyenAm.IndexOfAny(c4) < 0 || chu.nguyenAm.IndexOfAny(c10) >= 0)
			{
				return false;
			}
			chu.moc = '^';
			break;
		}
		case TypingMode.UThanh7OThanh7AThanh8:
		case TypingMode.UOASimple:
		{
			char[] d = new char[2] { 'a', 'A' };
			if (chu.nguyenAm.Length == 0 || chu.nguyenAm[0] == 'u' || chu.nguyenAm[0] == 'U' || (chu.nguyenAm.IndexOfAny(d) < 0 && chu.nguyenAm[0] == 'o') || chu.nguyenAm[0] == 'O')
			{
				char[] c5 = new char[4] { 'u', 'o', 'U', 'O' };
				if (chu.nguyenAm.IndexOfAny(c5) < 0)
				{
					return false;
				}
				if (chu.nguyenAm.Length == 1)
				{
					chu.UODau = true;
				}
				chu.moc = '*';
			}
			else
			{
				if (chu.nguyenAm.IndexOfAny(d) < 0)
				{
					return false;
				}
				chu.moc = '(';
			}
			break;
		}
		case TypingMode.UThanh7OThanh7:
		{
			char[] c6 = new char[4] { 'u', 'o', 'U', 'O' };
			if (chu.nguyenAm.IndexOfAny(c6) < 0)
			{
				return false;
			}
			if (chu.nguyenAm.Length == 1)
			{
				chu.UODau = true;
			}
			chu.moc = '*';
			break;
		}
		case TypingMode.AThanh8:
		{
			char[] c7 = new char[2] { 'a', 'A' };
			if (chu.nguyenAm.IndexOfAny(c7) < 0)
			{
				return false;
			}
			chu.moc = '(';
			break;
		}
		}
		if (chu.moc == '*' && mocCu == '*' && chu.UODau && chu.nguyenAm.Length == 2)
		{
			mocCu = '-';
			chu.UODau = false;
		}
		if (chu.moc == mocCu)
		{
			chu.trungdau = trungDau;
			chu.moc = '-';
			chu.vitriThanhPhan = 2;
			if (mocCu == '*' && (dnkg == TypingMode.UThanh7OThanh7AThanh8 || dnkg == TypingMode.UThanh7OThanh7))
			{
				chu.amCuoi += (kyTuGo != '\0' ? kyTuGo.ToString() : (chu.nguyenAm[0] == 'U' ? kieuGo[10].ToString().ToUpper() : kieuGo[10].ToString().ToLower()));
				if (chu.UOA)
				{
					chu.nguyenAm = chu.nguyenAm.Substring(1);
				}
			}
		}
		return true;
	}

	private void KiemTraViTriDau()
	{
		if (chu.nguyenAm.Length == 1)
		{
			chu.vitriDau = 0;
		}
		else if (chu.nguyenAm.Length == 2)
		{
			if (chu.nguyenAm[0] == 'a' || chu.nguyenAm[0] == 'A')
			{
				if ((chu.nguyenAm[1] != 'e' && chu.nguyenAm[1] != 'E') || (chu.nguyenAm[1] != 'a' && chu.nguyenAm[1] != 'A'))
				{
					chu.vitriDau = 0;
				}
				else
				{
					chu.vitriDau = -1;
				}
			}
			else if (chu.nguyenAm[0] == 'e' || chu.nguyenAm[0] == 'E')
			{
				if (chu.nguyenAm[1] == 'o' || chu.nguyenAm[1] == 'O' || chu.nguyenAm[1] == 'u' || chu.nguyenAm[1] == 'U' || chu.nguyenAm[1] == 'y' || chu.nguyenAm[1] == 'Y')
				{
					chu.vitriDau = 0;
				}
				else
				{
					chu.vitriDau = -1;
				}
			}
			else if (chu.nguyenAm[0] == 'i' || chu.nguyenAm[0] == 'I')
			{
				if (chu.nguyenAm[1] == 'e' || chu.nguyenAm[1] == 'E')
				{
					chu.vitriDau = 1;
				}
				else if (chu.nguyenAm[1] == 'u' || chu.nguyenAm[1] == 'U' || chu.nguyenAm[1] == 'a' || chu.nguyenAm[1] == 'A')
				{
					chu.vitriDau = 0;
				}
				else
				{
					chu.vitriDau = -1;
				}
			}
			else if (chu.nguyenAm[0] == 'o' || chu.nguyenAm[0] == 'O')
			{
				if (chu.nguyenAm[1] == 'a' || chu.nguyenAm[1] == 'A' || chu.nguyenAm[1] == 'e' || chu.nguyenAm[1] == 'E')
				{
					if (chu.amCuoi.Length > 0 || boDauKieuMoi || chu.moc == '^' || chu.moc == '(')
					{
						chu.vitriDau = 1;
					}
					else
					{
						chu.vitriDau = 0;
					}
				}
				else if (chu.nguyenAm[1] == 'i' || chu.nguyenAm[1] == 'I')
				{
					chu.vitriDau = 0;
				}
				else if (chu.nguyenAm[1] == 'o' || chu.nguyenAm[1] == 'O')
				{
					chu.vitriDau = 1;
				}
				else
				{
					chu.vitriDau = -1;
				}
			}
			else if (chu.nguyenAm[0] == 'u' || chu.nguyenAm[0] == 'U')
			{
				if (chu.nguyenAm[1] == 'a' || chu.nguyenAm[1] == 'A')
				{
					if (chu.amCuoi.Length > 0 || chu.moc == '^' || chu.moc == '(')
					{
						chu.vitriDau = 1;
					}
					else
					{
						chu.vitriDau = 0;
					}
				}
				else if (chu.nguyenAm[1] == 'e' || chu.nguyenAm[1] == 'E')
				{
					chu.vitriDau = 1;
				}
				else if (chu.nguyenAm[1] == 'i' || chu.nguyenAm[1] == 'I' || chu.nguyenAm[1] == 'u' || chu.nguyenAm[1] == 'U')
				{
					chu.vitriDau = 0;
				}
				else if (chu.nguyenAm[1] == 'y' || chu.nguyenAm[1] == 'Y')
				{
					if (chu.amCuoi.Length > 0 || boDauKieuMoi)
					{
						chu.vitriDau = 1;
					}
					else
					{
						chu.vitriDau = 0;
					}
				}
				else if (chu.nguyenAm[1] == 'o' || chu.nguyenAm[1] == 'O')
				{
					chu.vitriDau = 1;
				}
				else
				{
					chu.vitriDau = -1;
				}
			}
			else if (chu.nguyenAm[0] == 'y' || chu.nguyenAm[0] == 'Y')
			{
				chu.vitriDau = 1;
			}
			else
			{
				chu.vitriDau = -1;
			}
		}
		else if (chu.nguyenAm.Length == 3)
		{
			switch (chu.nguyenAm.Substring(0, 3).ToLower())
			{
			case "uye":
				chu.vitriDau = 2;
				break;
			case "uou":
			case "ieu":
			case "oai":
			case "uay":
			case "oay":
			case "uoi":
			case "uya":
			case "yeu":
			case "oeo":
			case "uyu":
				chu.vitriDau = 1;
				break;
			default:
				chu.vitriDau = -1;
				break;
			}
		}
		else
		{
			chu.vitriDau = -1;
		}
	}

	public bool TimKiemAmDau()
	{
		if (chu.amDau.ToLower() == "gi" && chu.nguyenAm.Length > 0 && "iI".IndexOf(chu.nguyenAm[0]) >= 0)
		{
			return false;
		}
		if (chu.amDau.ToLower() == "qu" && chu.nguyenAm.Length > 0 && "uU".IndexOf(chu.nguyenAm[0]) >= 0)
		{
			return false;
		}
		kq = -1;
		if (chu.amDau == "")
		{
			kq = 0;
		}
		else if ((chu.amDau == "k" || chu.amDau == "K") && chu.nguyenAm != "" && (chu.nguyenAm[0] == 'y' || chu.nguyenAm[0] == 'Y'))
		{
			kq = 3;
		}
		else
		{
			for (int i = 0; i < amDau.Length; i++)
			{
				if (amDau[i][0] == '/' && chu.amDau.ToLower() == amDau[i].Substring(1).ToLower())
				{
					kq = 1;
					break;
				}
				if (amDau[i][0] == '`' && chu.amDau.ToLower() == amDau[i].Substring(1).ToLower())
				{
					kq = 2;
					break;
				}
				if (chu.amDau.ToLower() == amDau[i].ToLower())
				{
					kq = 0;
					break;
				}
			}
		}
		if ((chu.amDau == "g" || chu.amDau == "G") && (chu.nguyenAm == "i" || chu.nguyenAm == "I"))
		{
			kq = 0;
		}
		if (kq < 0 || chu.nguyenAm == "" || (kq == 1 && "iIeE".IndexOf(chu.nguyenAm[0]) < 0) || (kq == 2 && "iIeE".IndexOf(chu.nguyenAm[0]) >= 0))
		{
			return false;
		}
		return true;
	}

	private string? ChuyenSangVietnameseProcessor()
	{
		KiemTraViTriDau();
		int dem = 0;
		int value = -1;
		string s = "";
		bool tv = false;
		if (chu.moc == '^')
		{
			char[] c = new char[2] { 'a', 'A' };
			char[] c4 = new char[2] { 'o', 'O' };
			char[] c5 = new char[2] { 'e', 'E' };
			char[] c6 = new char[6] { 'a', 'A', 'o', 'O', 'e', 'E' };
			if (chu.nguyenAm.IndexOfAny(c) >= 0 && chu.nguyenAm.IndexOfAny(c4) >= 0)
			{
				return null;
			}
			if (chu.nguyenAm.IndexOfAny(c4) >= 0 && chu.nguyenAm.IndexOfAny(c5) >= 0)
			{
				return null;
			}
			if (chu.nguyenAm.IndexOfAny(c5) >= 0 && chu.nguyenAm.IndexOfAny(c) >= 0)
			{
				return null;
			}
			if (chu.nguyenAm.IndexOfAny(c6) < 0)
			{
				return null;
			}
			for (int i = 0; i < chu.nguyenAm.Length; i++)
			{
				value = -1;
				if (chu.nguyenAm[i] == 'a')
				{
					value = 12;
				}
				else if (chu.nguyenAm[i] == 'A')
				{
					value = 85;
				}
				else if (chu.nguyenAm[i] == 'o')
				{
					value = 42;
				}
				else if (chu.nguyenAm[i] == 'O')
				{
					value = 115;
				}
				else if (chu.nguyenAm[i] == 'e')
				{
					value = 24;
				}
				else if (chu.nguyenAm[i] == 'E')
				{
					value = 97;
				}
				if (value >= 0)
				{
					if (chu.vitriDau == i)
					{
						value = (int)(value + chu.dau);
					}
					s += bangMa[value];
					tv = true;
				}
				else
				{
					s += chu.nguyenAm[i];
				}
			}
			if (tv)
			{
				return s;
			}
			return null;
		}
		if (chu.moc == '*')
		{
			char[] c2 = new char[4] { 'u', 'o', 'U', 'O' };
			if (chu.nguyenAm.IndexOfAny(c2) < 0)
			{
				return null;
			}
			dem = 0;
			for (int j = 0; j < chu.nguyenAm.Length; j++)
			{
				value = -1;
				if (chu.nguyenAm[j] == 'o')
				{
					value = 48;
				}
				else if (chu.nguyenAm[j] == 'O')
				{
					value = 121;
				}
				else if (chu.nguyenAm[j] == 'u')
				{
					value = ((!(chu.nguyenAm.ToLower() == "uo") || chu.amCuoi.Length != 0 || chu.amDau.Length <= 0) ? 60 : 54);
					dem++;
				}
				else if (chu.nguyenAm[j] == 'U')
				{
					value = ((!(chu.nguyenAm.ToLower() == "uo") || chu.amCuoi.Length != 0 || chu.amDau.Length <= 0) ? 133 : 127);
					dem++;
				}
				if (value >= 0 && dem < 2)
				{
					if (chu.vitriDau == j)
					{
						value = (int)(value + chu.dau);
					}
					s += bangMa[value];
					tv = true;
				}
				else
				{
					s += chu.nguyenAm[j];
				}
			}
			if (tv)
			{
				return s;
			}
			return null;
		}
		if (chu.moc == '(')
		{
			char[] c3 = new char[2] { 'a', 'A' };
			if (chu.nguyenAm.IndexOfAny(c3) < 0)
			{
				return null;
			}
			for (int k = 0; k < chu.nguyenAm.Length; k++)
			{
				value = -1;
				if (chu.nguyenAm[k] == 'a')
				{
					value = 6;
				}
				else if (chu.nguyenAm[k] == 'A')
				{
					value = 79;
				}
				if (value >= 0)
				{
					if (chu.vitriDau == k)
					{
						value = (int)(value + chu.dau);
					}
					s += bangMa[value];
					chu.moc = '(';
					tv = true;
				}
				else
				{
					s += chu.nguyenAm[k];
				}
			}
			if (tv)
			{
				return s;
			}
			return null;
		}
		for (int l = 0; l < chu.nguyenAm.Length; l++)
		{
			value = -1;
			if (chu.nguyenAm[l] == 'a')
			{
				value = 0;
			}
			else if (chu.nguyenAm[l] == 'A')
			{
				value = 73;
			}
			else if (chu.nguyenAm[l] == 'o')
			{
				value = 36;
			}
			else if (chu.nguyenAm[l] == 'O')
			{
				value = 109;
			}
			else if (chu.nguyenAm[l] == 'e')
			{
				value = 18;
			}
			else if (chu.nguyenAm[l] == 'E')
			{
				value = 91;
			}
			else if (chu.nguyenAm[l] == 'i')
			{
				value = 30;
			}
			else if (chu.nguyenAm[l] == 'I')
			{
				value = 103;
			}
			else if (chu.nguyenAm[l] == 'u')
			{
				value = 54;
			}
			else if (chu.nguyenAm[l] == 'U')
			{
				value = 127;
			}
			else if (chu.nguyenAm[l] == 'y')
			{
				value = 66;
			}
			else if (chu.nguyenAm[l] == 'Y')
			{
				value = 139;
			}
			if (value >= 0)
			{
				if (chu.vitriDau == l)
				{
					value = (int)(value + chu.dau);
				}
				s += bangMa[value];
				tv = true;
			}
			else
			{
				s += chu.nguyenAm[l];
			}
		}
		if (tv)
		{
			return s;
		}
		return null;
	}

	public bool KiemTraNguyenAm(char c)
	{
		if ("aAeEiIoOuUyY".IndexOf(c) >= 0)
		{
			return true;
		}
		return false;
	}

	public TypingMode TimKiemKieuGo(char tim)
	{
		for (int i = 0; i < kieuGo.Length; i++)
		{
			if (kieuGo[i] != ' ' && kieuGo[i].ToString().ToLower() == tim.ToString().ToLower())
			{
				return (TypingMode)i;
			}
		}
		if (kieuGo.Length > 10 && kieuGo[10] == 'w')
		{
			if (tim is ']' or '}')
			{
				return TypingMode.UThanh7OThanh7AThanh8;
			}
			if (tim is '[' or '{')
			{
				return TypingMode.UThanh7OThanh7;
			}
		}
		return TypingMode.Null;
	}

	public bool TimKiemPhuAmCuoi()
	{
		if (chu.amCuoi == "")
		{
			return true;
		}
		stringChinhTa = chu.amCuoi.ToLower();
		for (int i = 0; i < phuAmCuoi.Length; i++)
		{
			if (stringChinhTa == phuAmCuoi[i])
			{
				return true;
			}
		}
		if (stringChinhTa == "k")
		{
			return true;
		}
		if (ktChinhTa == 2 || (!(stringChinhTa == "h") && !(stringChinhTa == "k")))
		{
			return false;
		}
		return true;
	}

	public bool ChiSacNang()
	{
		stringChinhTa = chu.amCuoi.ToLower();
		for (int i = 0; i < sacNang.Length; i++)
		{
			if (stringChinhTa == sacNang[i])
			{
				return true;
			}
		}
		return false;
	}

	public bool KiemTraChinhTa()
	{
		stringChinhTa = chu.nguyenAm.ToLower() + chu.amCuoi.ToLower();
		if (chu.nguyenAm == "" || (chu.D9 && chu.nguyenAm != "" && !KiemTraNguyenAm(chu.nguyenAm[0])))
		{
			return true;
		}
		if (chu.amDau.ToLower() == "gi" && "iI".IndexOf(chu.nguyenAm[0]) >= 0)
		{
			return false;
		}
		if (chu.amDau.ToLower() == "qu" && "uU".IndexOf(chu.nguyenAm[0]) >= 0)
		{
			return false;
		}
		kq = -1;
		if (ktChinhTa == 1 && TimKiemPhuAmCuoi())
		{
			return true;
		}
		if (chu.amDau == "")
		{
			kq = 0;
		}
		else if ((chu.amDau == "k" || chu.amDau == "K") && chu.nguyenAm != "" && (chu.nguyenAm[0] == 'y' || chu.nguyenAm[0] == 'Y'))
		{
			kq = 3;
		}
		else
		{
			this.i = 0;
			while (this.i < amDau.Length)
			{
				if (amDau[this.i][0] == '/' && chu.amDau.ToLower() == amDau[this.i].Substring(1).ToLower())
				{
					kq = 1;
					break;
				}
				if (amDau[this.i][0] == '`' && chu.amDau.ToLower() == amDau[this.i].Substring(1).ToLower())
				{
					kq = 2;
					break;
				}
				if (chu.amDau.ToLower() == amDau[this.i].ToLower())
				{
					kq = 0;
					break;
				}
				this.i++;
			}
		}
		if ((chu.amDau == "g" || chu.amDau == "G") && (chu.nguyenAm == "i" || chu.nguyenAm == "I"))
		{
			kq = 0;
		}
		if (kq < 0 || chu.nguyenAm == "" || (kq == 1 && "iIeE".IndexOf(chu.nguyenAm[0]) < 0) || (kq == 2 && "iIeE".IndexOf(chu.nguyenAm[0]) >= 0))
		{
			return false;
		}
		for (int i = 0; i < van.Length; i += 2)
		{
			if (stringChinhTa == van[i].ToLower())
			{
				if (van[i] == "a" && (chu.moc == '^' || chu.moc == '(') && chu.amDau.Length > 0 && chu.amCuoi.Length == 0)
				{
					return false;
				}
				if (van[i + 1][0] == chu.moc && ((ChiSacNang() && (chu.dau == Dau.sac || chu.dau == Dau.nang)) || !ChiSacNang()))
				{
					return true;
				}
			}
		}
		return false;
	}
}
