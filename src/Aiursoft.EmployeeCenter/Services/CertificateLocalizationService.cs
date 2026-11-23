using System.Globalization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.CertificateViewModels;
using Aiursoft.Scanner.Abstractions;

namespace Aiursoft.EmployeeCenter.Services;

public record CertificateContent(
    string Title,
    string Paragraph1Html,
    string Paragraph2Html,
    string DisplayedCompanyName,
    string FormattedIssueDate);

public class CertificateLocalizationService : ITransientDependency
{
    public static readonly Dictionary<string, string> SupportedCultures = new()
    {
        { "en-US", "English (United States)" },
        { "en-GB", "English (United Kingdom)" },
        { "zh-CN", "中文 (中国大陆)" },
        { "zh-TW", "中文 (台灣)" },
        { "zh-HK", "中文 (香港)" },
        { "ja-JP", "日本語 (日本)" },
        { "ko-KR", "한국어 (대한민국)" },
        { "vi-VN", "Tiếng Việt (Việt Nam)" },
        { "th-TH", "ภาษาไทย (ประเทศไทย)" },
        { "de-DE", "Deutsch (Deutschland)" },
        { "fr-FR", "Français (France)" },
        { "es-ES", "Español (España)" },
        { "ru-RU", "Русский (Россия)" },
        { "it-IT", "Italiano (Italia)" },
        { "pt-PT", "Português (Portugal)" },
        { "pt-BR", "Português (Brasil)" },
        { "ar-SA", "العربية (المملكة العربية السعودية)" },
        { "nl-NL", "Nederlands (Nederland)" },
        { "sv-SE", "Svenska (Sverige)" },
        { "pl-PL", "Polski (Polska)" },
        { "tr-TR", "Türkçe (Türkiye)" },
        { "ro-RO", "Română (România)" },
        { "da-DK", "Dansk (Danmark)" },
        { "uk-UA", "Українська (Україна)" },
        { "id-ID", "Bahasa Indonesia (Indonesia)" },
        { "fi-FI", "Suomi (Suomi)" },
        { "hi-IN", "हिन्दी (भारत)" },
        { "el-GR", "Ελληνικά (Ελλάδα)" },
        { "hr-HR", "Hrvatski (Hrvatska)" }
    };

    public CertificateContent BuildCertificate(
        CertificateType type,
        string? requestedCulture,
        User user,
        string companyName,
        string? companyNameEnglish,
        DateTime issueDate)
    {
        var cultureName = NormalizeCulture(requestedCulture);
        var culture = CultureInfo.GetCultureInfo(cultureName);

        var isChinese = cultureName is "zh-CN" or "zh-TW" or "zh-HK";
        var displayedCompanyName = (!isChinese && !string.IsNullOrWhiteSpace(companyNameEnglish))
            ? companyNameEnglish
            : companyName;

        var employeeName = user.LegalName ?? user.DisplayName;
        var employeeTitle = user.Title;
        var startDateString = FormatStartDate(user.CreationTime, cultureName, culture);
        var issueDateString = FormatIssueDate(issueDate, cultureName, culture);
        var salaryString = user.BaseSalary.ToString("N2", culture);

        var (title, p1, p2) = GetLocalizedTexts(
            type,
            cultureName,
            employeeName,
            displayedCompanyName,
            startDateString,
            employeeTitle,
            salaryString);

        return new CertificateContent(
            Title: title,
            Paragraph1Html: p1,
            Paragraph2Html: p2,
            DisplayedCompanyName: displayedCompanyName,
            FormattedIssueDate: issueDateString);
    }

    public static string NormalizeCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return "zh-CN";
        }

        var normalized = culture.Trim();
        if (SupportedCultures.ContainsKey(normalized))
        {
            return normalized;
        }

        // Try loose matching e.g. "hr" -> "hr-HR"
        var match = SupportedCultures.Keys
            .FirstOrDefault(k => k.StartsWith(normalized, StringComparison.OrdinalIgnoreCase) ||
                                 k.Split('-')[0].Equals(normalized, StringComparison.OrdinalIgnoreCase));

        return match ?? "en-US";
    }

    private static string FormatStartDate(DateTime date, string cultureName, CultureInfo culture)
    {
        return cultureName switch
        {
            "zh-CN" or "zh-TW" or "zh-HK" => date.ToString("yyyy年MM月dd日", CultureInfo.InvariantCulture),
            "ja-JP" => date.ToString("yyyy年M月d日", CultureInfo.InvariantCulture),
            "ko-KR" => date.ToString("yyyy년 M월 d일", CultureInfo.InvariantCulture),
            "ar-SA" => date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
            "en-US" => date.ToString("MMMM dd, yyyy", culture),
            "en-GB" => date.ToString("dd MMMM yyyy", culture),
            _ => date.ToString("d", culture)
        };
    }

    private static string FormatIssueDate(DateTime date, string cultureName, CultureInfo culture)
    {
        return cultureName switch
        {
            "zh-CN" or "zh-TW" or "zh-HK" => date.ToString("yyyy年MM月dd日", CultureInfo.InvariantCulture),
            "ja-JP" => date.ToString("yyyy年M月d日", CultureInfo.InvariantCulture),
            "ko-KR" => date.ToString("yyyy년 M월 d일", CultureInfo.InvariantCulture),
            "ar-SA" => date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
            "en-US" => date.ToString("MMMM dd, yyyy", culture),
            "en-GB" => date.ToString("dd MMMM yyyy", culture),
            _ => date.ToString("d", culture)
        };
    }

    private static (string Title, string Paragraph1Html, string Paragraph2Html) GetLocalizedTexts(
        CertificateType type,
        string cultureName,
        string name,
        string company,
        string startDate,
        string? title,
        string salary)
    {
        return (cultureName, type) switch
        {
            // 1. zh-CN
            ("zh-CN", CertificateType.Employment) => (
                "在职证明",
                $"兹证明 <strong>{name}</strong> 先生/女士，自 <strong>{startDate}</strong> 起至今在我公司任职，担任 <strong>{title ?? "员工"}</strong> 一职。",
                "特此证明。"
            ),
            ("zh-CN", CertificateType.Income) => (
                "收入证明",
                $"兹证明 <strong>{name}</strong> 先生/女士，自 <strong>{startDate}</strong> 起至今在我公司任职，担任 <strong>{title ?? "员工"}</strong> 一职。其月薪为人民币 <strong>{salary}</strong> 元。",
                "特此证明。"
            ),

            // 2. zh-TW
            ("zh-TW", CertificateType.Employment) => (
                "在職證明",
                $"茲證明 <strong>{name}</strong> 先生/女士，自 <strong>{startDate}</strong> 起至今在我公司任職，擔任 <strong>{title ?? "員工"}</strong> 一職。",
                "特此證明。"
            ),
            ("zh-TW", CertificateType.Income) => (
                "收入證明",
                $"茲證明 <strong>{name}</strong> 先生/女士，自 <strong>{startDate}</strong> 起至今在我公司任職，擔任 <strong>{title ?? "員工"}</strong> 一職。其月薪為人民幣 <strong>{salary}</strong> 元。",
                "特此證明。"
            ),

            // 3. zh-HK
            ("zh-HK", CertificateType.Employment) => (
                "在職證明",
                $"茲證明 <strong>{name}</strong> 先生/女士，自 <strong>{startDate}</strong> 起至今在我公司任職，擔任 <strong>{title ?? "員工"}</strong> 一職。",
                "特此證明。"
            ),
            ("zh-HK", CertificateType.Income) => (
                "收入證明",
                $"茲證明 <strong>{name}</strong> 先生/女士，自 <strong>{startDate}</strong> 起至今在我公司任職，擔任 <strong>{title ?? "員工"}</strong> 一職。其月薪為人民幣 <strong>{salary}</strong> 元。",
                "特此證明。"
            ),

            // 4. hr-HR
            ("hr-HR", CertificateType.Employment) => (
                "Potvrda o zaposlenju",
                $"Potvrđuje se da je osoba <strong>{name}</strong> zaposlena u društvu <strong>{company}</strong> od <strong>{startDate}</strong> i trenutačno radi na radnom mjestu <strong>{title ?? "zaposlenik"}</strong>.",
                "Potvrda se izdaje na zahtjev zaposlenika."
            ),
            ("hr-HR", CertificateType.Income) => (
                "Potvrda o primanjima",
                $"Potvrđuje se da je osoba <strong>{name}</strong> zaposlena u društvu <strong>{company}</strong> od <strong>{startDate}</strong> i trenutačno radi na radnom mjestu <strong>{title ?? "zaposlenik"}</strong>. Mjesečna plaća iznosi <strong>{salary} RMB</strong>.",
                "Potvrda se izdaje na zahtjev zaposlenika."
            ),

            // 5. ja-JP
            ("ja-JP", CertificateType.Employment) => (
                "在職証明書",
                $"<strong>{name}</strong> 氏は、<strong>{startDate}</strong> より当社 <strong>{company}</strong> に在籍し、<strong>{title ?? "社員"}</strong> として勤務していることを証明します。",
                "本証明書は本人の請求に基づき発行されたものです。"
            ),
            ("ja-JP", CertificateType.Income) => (
                "給与証明書",
                $"<strong>{name}</strong> 氏は、<strong>{startDate}</strong> より当社 <strong>{company}</strong> に在籍し、<strong>{title ?? "社員"}</strong> として勤務していることを証明します。月給は <strong>{salary} 人民元</strong> です。",
                "本証明書は本人の請求に基づき発行されたものです。"
            ),

            // 6. ko-KR
            ("ko-KR", CertificateType.Employment) => (
                "재직증명서",
                $"<strong>{name}</strong> 귀하는 <strong>{startDate}</strong>부터 현재까지 당사 <strong>{company}</strong>에 재직하며 <strong>{title ?? "직원"}</strong>(으)로 근무하고 있음을 증명합니다.",
                "본 증명서는 직원의 요청에 따라 발급되었습니다."
            ),
            ("ko-KR", CertificateType.Income) => (
                "소득증명서",
                $"<strong>{name}</strong> 귀하는 <strong>{startDate}</strong>부터 현재까지 당사 <strong>{company}</strong>에 재직하며 <strong>{title ?? "직원"}</strong>(으)로 근무하고 있음을 증명합니다. 월 급여는 <strong>{salary} RMB</strong>입니다.",
                "본 증명서는 직원의 요청에 따라 발급되었습니다."
            ),

            // 7. de-DE
            ("de-DE", CertificateType.Employment) => (
                "Arbeitsbescheinigung",
                $"Hiermit wird bestätigt, dass Herr/Frau <strong>{name}</strong> seit dem <strong>{startDate}</strong> bei <strong>{company}</strong> beschäftigt ist und derzeit die Position als <strong>{title ?? "Mitarbeiter/in"}</strong> innehat.",
                "Diese Bescheinigung wird auf Antrag des Arbeitnehmers ausgestellt."
            ),
            ("de-DE", CertificateType.Income) => (
                "Einkommensbescheinigung",
                $"Hiermit wird bestätigt, dass Herr/Frau <strong>{name}</strong> seit dem <strong>{startDate}</strong> bei <strong>{company}</strong> beschäftigt ist und derzeit die Position als <strong>{title ?? "Mitarbeiter/in"}</strong> innehat. Das monatliche Gehalt beträgt <strong>{salary} RMB</strong>.",
                "Diese Bescheinigung wird auf Antrag des Arbeitnehmers ausgestellt."
            ),

            // 8. fr-FR
            ("fr-FR", CertificateType.Employment) => (
                "Certificat de travail",
                $"Il est certifié par la présente que M./Mme <strong>{name}</strong> est employé(e) par la société <strong>{company}</strong> depuis le <strong>{startDate}</strong> et occupe actuellement le poste de <strong>{title ?? "Employé(e)"}</strong>.",
                "Cette attestation est délivrée à la demande de l'employé(e)."
            ),
            ("fr-FR", CertificateType.Income) => (
                "Attestation de revenus",
                $"Il est certifié par la présente que M./Mme <strong>{name}</strong> est employé(e) par la société <strong>{company}</strong> depuis le <strong>{startDate}</strong> et occupe actuellement le poste de <strong>{title ?? "Employé(e)"}</strong>. Son salaire mensuel est de <strong>{salary} RMB</strong>.",
                "Cette attestation est délivrée à la demande de l'employé(e)."
            ),

            // 9. es-ES
            ("es-ES", CertificateType.Employment) => (
                "Certificado de empleo",
                $"Por la presente se certifica que D./Dña. <strong>{name}</strong> trabaja en <strong>{company}</strong> desde el <strong>{startDate}</strong> y desempeña actualmente el cargo de <strong>{title ?? "Empleado/a"}</strong>.",
                "El presente certificado se expide a petición del empleado."
            ),
            ("es-ES", CertificateType.Income) => (
                "Certificado de ingresos",
                $"Por la presente se certifica que D./Dña. <strong>{name}</strong> trabaja en <strong>{company}</strong> desde el <strong>{startDate}</strong> y desempeña actualmente el cargo de <strong>{title ?? "Empleado/a"}</strong>. Su salario mensual es de <strong>{salary} RMB</strong>.",
                "El presente certificado se expide a petición del empleado."
            ),

            // 10. ru-RU
            ("ru-RU", CertificateType.Employment) => (
                "Справка с места работы",
                $"Настоящим подтверждается, что <strong>{name}</strong> работает в компании <strong>{company}</strong> с <strong>{startDate}</strong> и в настоящее время занимает должность <strong>{title ?? "Сотрудник"}</strong>.",
                "Справка выдана по месту требования работника."
            ),
            ("ru-RU", CertificateType.Income) => (
                "Справка о доходах",
                $"Настоящим подтверждается, что <strong>{name}</strong> работает в компании <strong>{company}</strong> с <strong>{startDate}</strong> и в настоящее время занимает должность <strong>{title ?? "Сотрудник"}</strong>. Ежемесячный оклад составляет <strong>{salary} RMB</strong>.",
                "Справка выдана по месту требования работника."
            ),

            // 11. it-IT
            ("it-IT", CertificateType.Employment) => (
                "Certificato di servizio",
                $"Si certifica che il/la sig./sig.ra <strong>{name}</strong> è dipendente della società <strong>{company}</strong> dal <strong>{startDate}</strong> e ricopre attualmente la posizione di <strong>{title ?? "Dipendente"}</strong>.",
                "Il presente certificato viene rilasciato su richiesta del dipendente."
            ),
            ("it-IT", CertificateType.Income) => (
                "Certificato di retribuzione",
                $"Si certifica che il/la sig./sig.ra <strong>{name}</strong> è dipendente della società <strong>{company}</strong> dal <strong>{startDate}</strong> e ricopre attualmente la posizione di <strong>{title ?? "Dipendente"}</strong>. Lo stipendio mensile è pari a <strong>{salary} RMB</strong>.",
                "Il presente certificato viene rilasciato su richiesta del dipendente."
            ),

            // 12. pt-PT
            ("pt-PT", CertificateType.Employment) => (
                "Declaração de emprego",
                $"Declara-se para os devidos efeitos que o(a) Sr.(a) <strong>{name}</strong> trabalha na empresa <strong>{company}</strong> desde <strong>{startDate}</strong>, exercendo atualmente a função de <strong>{title ?? "Colaborador(a)"}</strong>.",
                "A presente declaração é emitida a pedido do(a) trabalhador(a)."
            ),
            ("pt-PT", CertificateType.Income) => (
                "Declaração de rendimentos",
                $"Declara-se para os devidos efeitos que o(a) Sr.(a) <strong>{name}</strong> trabalha na empresa <strong>{company}</strong> desde <strong>{startDate}</strong>, exercendo atualmente a função de <strong>{title ?? "Colaborador(a)"}</strong>. O seu salário mensal é de <strong>{salary} RMB</strong>.",
                "A presente declaração é emitida a pedido do(a) trabalhador(a)."
            ),

            // 13. pt-BR
            ("pt-BR", CertificateType.Employment) => (
                "Declaração de emprego",
                $"Declara-se para os devidos fins que o(a) Sr.(a) <strong>{name}</strong> é empregado(a) da empresa <strong>{company}</strong> desde <strong>{startDate}</strong>, ocupando atualmente o cargo de <strong>{title ?? "Colaborador(a)"}</strong>.",
                "A presente declaração é emitida a pedido do(a) colaborador(a)."
            ),
            ("pt-BR", CertificateType.Income) => (
                "Declaração de renda",
                $"Declara-se para os devidos fins que o(a) Sr.(a) <strong>{name}</strong> é empregado(a) da empresa <strong>{company}</strong> desde <strong>{startDate}</strong>, ocupando atualmente o cargo de <strong>{title ?? "Colaborador(a)"}</strong>. Seu salário mensal é de <strong>RMB {salary}</strong>.",
                "A presente declaração é emitida a pedido do(a) colaborador(a)."
            ),

            // 14. nl-NL
            ("nl-NL", CertificateType.Employment) => (
                "Werkgeversverklaring",
                $"Hierbij wordt verklaard dat dhr./mevr. <strong>{name}</strong> sinds <strong>{startDate}</strong> in dienst is bij <strong>{company}</strong> en momenteel de functie bekleedt van <strong>{title ?? "Medewerker"}</strong>.",
                "Deze verklaring is afgegeven op verzoek van de werknemer."
            ),
            ("nl-NL", CertificateType.Income) => (
                "Inkomensverklaring",
                $"Hierbij wordt verklaard dat dhr./mevr. <strong>{name}</strong> sinds <strong>{startDate}</strong> in dienst is bij <strong>{company}</strong> en momenteel de functie bekleedt van <strong>{title ?? "Medewerker"}</strong>. Het maandsalaris bedraagt <strong>{salary} RMB</strong>.",
                "Deze verklaring is afgegeven op verzoek van de werknemer."
            ),

            // 15. pl-PL
            ("pl-PL", CertificateType.Employment) => (
                "Zaświadczenie o zatrudnieniu",
                $"Niniejszym zaświadcza się, że Pan/Pani <strong>{name}</strong> jest zatrudniony(-a) w firmie <strong>{company}</strong> od dnia <strong>{startDate}</strong> i obecnie zajmuje stanowisko <strong>{title ?? "Pracownik"}</strong>.",
                "Zaświadczenie wydaje się na prośbę pracownika."
            ),
            ("pl-PL", CertificateType.Income) => (
                "Zaświadczenie o zarobkach",
                $"Niniejszym zaświadcza się, że Pan/Pani <strong>{name}</strong> jest zatrudniony(-a) w firmie <strong>{company}</strong> od dnia <strong>{startDate}</strong> i obecnie zajmuje stanowisko <strong>{title ?? "Pracownik"}</strong>. Miesięczne wynagrodzenie wynosi <strong>{salary} RMB</strong>.",
                "Zaświadczenie wydaje się na prośbę pracownika."
            ),

            // 16. tr-TR
            ("tr-TR", CertificateType.Employment) => (
                "Çalışma Belgesi",
                $"İşbu belge ile Sn. <strong>{name}</strong>'in <strong>{startDate}</strong> tarihinden itibaren <strong>{company}</strong> bünyesinde çalıştığı ve şu anda <strong>{title ?? "Çalışan"}</strong> pozisyonunda görev yaptığı tasdik olunur.",
                "Bu belge çalışanın talebi üzerine düzenlenmiştir."
            ),
            ("tr-TR", CertificateType.Income) => (
                "Gelir Belgesi",
                $"İşbu belge ile Sn. <strong>{name}</strong>'in <strong>{startDate}</strong> tarihinden itibaren <strong>{company}</strong> bünyesinde çalıştığı ve şu anda <strong>{title ?? "Çalışan"}</strong> pozisyonunda görev yaptığı tasdik olunur. Aylık maaşı <strong>{salary} RMB</strong>'dir.",
                "Bu belge çalışanın talebi üzerine düzenlenmiştir."
            ),

            // 17. uk-UA
            ("uk-UA", CertificateType.Employment) => (
                "Довідка з місця роботи",
                $"Цим підтверджується, що <strong>{name}</strong> працює в компанії <strong>{company}</strong> з <strong>{startDate}</strong> та наразі обіймає посаду <strong>{title ?? "Співробітник"}</strong>.",
                "Довідка видана на вимогу працівника."
            ),
            ("uk-UA", CertificateType.Income) => (
                "Довідка про доходи",
                $"Цим підтверджується, що <strong>{name}</strong> працює в компанії <strong>{company}</strong> з <strong>{startDate}</strong> та наразі обіймає посаду <strong>{title ?? "Співробітник"}</strong>. Щомісячний оклад становить <strong>{salary} RMB</strong>.",
                "Довідка видана на вимогу працівника."
            ),

            // 18. vi-VN
            ("vi-VN", CertificateType.Employment) => (
                "Giấy xác nhận công tác",
                $"Chứng nhận ông/bà <strong>{name}</strong> làm việc tại công ty <strong>{company}</strong> từ ngày <strong>{startDate}</strong> và hiện đang giữ chức vụ <strong>{title ?? "Nhân viên"}</strong>.",
                "Giấy chứng nhận này được cấp theo yêu cầu của nhân viên."
            ),
            ("vi-VN", CertificateType.Income) => (
                "Giấy xác nhận thu nhập",
                $"Chứng nhận ông/bà <strong>{name}</strong> làm việc tại công ty <strong>{company}</strong> từ ngày <strong>{startDate}</strong> và hiện đang giữ chức vụ <strong>{title ?? "Nhân viên"}</strong>. Mức lương hàng tháng là <strong>{salary} RMB</strong>.",
                "Giấy chứng nhận này được cấp theo yêu cầu của nhân viên."
            ),

            // 19. th-TH
            ("th-TH", CertificateType.Employment) => (
                "หนังสือรับรองการทำงาน",
                $"หนังสือฉบับนี้ให้ไว้เพื่อรับรองว่า คุณ <strong>{name}</strong> ได้เข้าทำงานที่ <strong>{company}</strong> ตั้งแต่วันที่ <strong>{startDate}</strong> และปัจจุบันดำรงตำแหน่ง <strong>{title ?? "พนักงาน"}</strong>",
                "หนังสือรับรองนี้ออกให้ตามความประสงค์ของพนักงาน"
            ),
            ("th-TH", CertificateType.Income) => (
                "หนังสือรับรองเงินเดือน",
                $"หนังสือฉบับนี้ให้ไว้เพื่อรับรองว่า คุณ <strong>{name}</strong> ได้เข้าทำงานที่ <strong>{company}</strong> ตั้งแต่วันที่ <strong>{startDate}</strong> และปัจจุบันดำรงตำแหน่ง <strong>{title ?? "พนักงาน"}</strong> มีเงินเดือนประจำเดือนละ <strong>{salary} RMB</strong>",
                "หนังสือรับรองนี้ออกให้ตามความประสงค์ของพนักงาน"
            ),

            // 20. id-ID
            ("id-ID", CertificateType.Employment) => (
                "Surat Keterangan Kerja",
                $"Dengan ini menerangkan bahwa Sdr/Sdri <strong>{name}</strong> telah bekerja pada <strong>{company}</strong> sejak <strong>{startDate}</strong> dan saat ini menjabat sebagai <strong>{title ?? "Karyawan"}</strong>.",
                "Surat keterangan ini diterbitkan atas permintaan karyawan yang bersangkutan."
            ),
            ("id-ID", CertificateType.Income) => (
                "Surat Keterangan Penghasilan",
                $"Dengan ini menerangkan bahwa Sdr/Sdri <strong>{name}</strong> telah bekerja pada <strong>{company}</strong> sejak <strong>{startDate}</strong> dan saat ini menjabat sebagai <strong>{title ?? "Karyawan"}</strong>. Penghasilan per bulan adalah <strong>RMB {salary}</strong>.",
                "Surat keterangan ini diterbitkan atas permintaan karyawan yang bersangkutan."
            ),

            // 21. sv-SE
            ("sv-SE", CertificateType.Employment) => (
                "Tjänstgöringsintyg",
                $"Härmed intygas att <strong>{name}</strong> har varit anställd vid <strong>{company}</strong> sedan <strong>{startDate}</strong> och för närvarande innehar tjänsten som <strong>{title ?? "Anställd"}</strong>.",
                "Detta intyg utfärdas på begäran av den anställde."
            ),
            ("sv-SE", CertificateType.Income) => (
                "Inkomstintyg",
                $"Härmed intygas att <strong>{name}</strong> har varit anställd vid <strong>{company}</strong> sedan <strong>{startDate}</strong> och för närvarande innehar tjänsten som <strong>{title ?? "Anställd"}</strong>. Månadslönen uppgår till <strong>{salary} RMB</strong>.",
                "Detta intyg utfärdas på begäran av den anställde."
            ),

            // 22. da-DK
            ("da-DK", CertificateType.Employment) => (
                "Ansættelsesbekræftelse",
                $"Det bekræftes hermed, at <strong>{name}</strong> har været ansat hos <strong>{company}</strong> siden <strong>{startDate}</strong> og p.t. bestrider stillingen som <strong>{title ?? "Medarbejder"}</strong>.",
                "Denne bekræftelse udstedes efter anmodning fra medarbejderen."
            ),
            ("da-DK", CertificateType.Income) => (
                "Indkomstbekræftelse",
                $"Det bekræftes hermed, at <strong>{name}</strong> har været ansat hos <strong>{company}</strong> siden <strong>{startDate}</strong> og p.t. bestrider stillingen som <strong>{title ?? "Medarbejder"}</strong>. Den månedlige løn udgør <strong>{salary} RMB</strong>.",
                "Denne bekræftelse udstedes efter anmodning fra medarbejderen."
            ),

            // 23. fi-FI
            ("fi-FI", CertificateType.Employment) => (
                "Työtodistus",
                $"Täten todistetaan, että <strong>{name}</strong> on työskennellyt <strong>{company}</strong> -yhtiössä <strong>{startDate}</strong> alkaen ja toimii tällä hetkellä tehtävässä <strong>{title ?? "Työntekijä"}</strong>.",
                "Tämä todistus on myönnetty työntekijän pyynnöstä."
            ),
            ("fi-FI", CertificateType.Income) => (
                "Palkkatodistus",
                $"Täten todistetaan, että <strong>{name}</strong> on työskennellyt <strong>{company}</strong> -yhtiössä <strong>{startDate}</strong> alkaen ja toimii tällä hetkellä tehtävässä <strong>{title ?? "Työntekijä"}</strong>. Kuukausipalkka on <strong>{salary} RMB</strong>.",
                "Tämä todistus on myönnetty työntekijän pyynnöstä."
            ),

            // 24. ro-RO
            ("ro-RO", CertificateType.Employment) => (
                "Adeverință de salariat",
                $"Prin prezenta se adeverește că dl/dna <strong>{name}</strong> este angajat(ă) al/a societății <strong>{company}</strong> din data de <strong>{startDate}</strong>, ocupând în prezent postul de <strong>{title ?? "Angajat"}</strong>.",
                "Prezenta adeverință a fost eliberată la cererea angajatului."
            ),
            ("ro-RO", CertificateType.Income) => (
                "Adeverință de venit",
                $"Prin prezenta se adeverește că dl/dna <strong>{name}</strong> este angajat(ă) al/a societății <strong>{company}</strong> din data de <strong>{startDate}</strong>, ocupând în prezent postul de <strong>{title ?? "Angajat"}</strong>. Salariul lunar este de <strong>{salary} RMB</strong>.",
                "Prezenta adeverință a fost eliberată la cererea angajatului."
            ),

            // 25. el-GR
            ("el-GR", CertificateType.Employment) => (
                "Βεβαίωση Εργασίας",
                $"Βεβαιώνεται ότι ο/η κ./κα <strong>{name}</strong> εργάζεται στην εταιρεία <strong>{company}</strong> από <strong>{startDate}</strong> και κατέχει σήμερα τη θέση <strong>{title ?? "Υπάλληλος"}</strong>.",
                "Η παρούσα βεβαίωση χορηγείται κατόπιν αιτήσεως του/της εργαζομένου/ης."
            ),
            ("el-GR", CertificateType.Income) => (
                "Βεβαίωση Αποδοχών",
                $"Βεβαιώνεται ότι ο/η κ./κα <strong>{name}</strong> εργάζεται στην εταιρεία <strong>{company}</strong> από <strong>{startDate}</strong> και κατέχει σήμερα τη θέση <strong>{title ?? "Υπάλληλος"}</strong>. Οι μηνιαίες αποδοχές ανέρχονται σε <strong>{salary} RMB</strong>.",
                "Η παρούσα βεβαίωση χορηγείται κατόπιν αιτήσεως του/της εργαζομένου/ης."
            ),

            // 26. hi-IN
            ("hi-IN", CertificateType.Employment) => (
                "रोजगार प्रमाण पत्र",
                $"प्रमाणित किया जाता है कि श्री/श्रीमती <strong>{name}</strong> <strong>{startDate}</strong> से हमारी कंपनी <strong>{company}</strong> में कार्यरत हैं तथा वर्तमान में <strong>{title ?? "कर्मचारी"}</strong> के पद पर कार्यरत हैं।",
                "यह प्रमाण पत्र कर्मचारी के अनुरोध पर जारी किया गया है।"
            ),
            ("hi-IN", CertificateType.Income) => (
                "आय प्रमाण पत्र",
                $"प्रमाणित किया जाता है कि श्री/श्रीमती <strong>{name}</strong> <strong>{startDate}</strong> से हमारी कंपनी <strong>{company}</strong> में कार्यरत हैं तथा वर्तमान में <strong>{title ?? "कर्मचारी"}</strong> के पद पर कार्यरत हैं। इनका मासिक वेतन <strong>{salary} RMB</strong> है।",
                "यह प्रमाण पत्र कर्मचारी के अनुरोध पर जारी किया गया है।"
            ),

            // 27. ar-SA
            ("ar-SA", CertificateType.Employment) => (
                "شهادة عمل",
                $"تشهد الشركة بأن السيد/السيدة <strong>{name}</strong> يعمل لدى شركة <strong>{company}</strong> منذ <strong>{startDate}</strong> ويشغل حالياً منصب <strong>{title ?? "موظف"}</strong>.",
                "تم إصدار هذه الشهادة بناءً على طلب الموظف."
            ),
            ("ar-SA", CertificateType.Income) => (
                "شهادة راتب",
                $"تشهد الشركة بأن السيد/السيدة <strong>{name}</strong> يعمل لدى شركة <strong>{company}</strong> منذ <strong>{startDate}</strong> ويشغل حالياً منصب <strong>{title ?? "موظف"}</strong>، ويبلغ راتبه الشهري <strong>{salary} RMB</strong>.",
                "تم إصدار هذه الشهادة بناءً على طلب الموظف."
            ),

            // 28. en-US / en-GB / default fallback
            (_, CertificateType.Income) => (
                "Certificate of Income",
                $"This is to certify that Mr./Ms. <strong>{name}</strong> has been employed by <strong>{company}</strong> since <strong>{startDate}</strong>. He/She currently holds the position of <strong>{title ?? "Employee"}</strong>. His/Her monthly salary is <strong>RMB {salary}</strong>.",
                "This certificate is issued at the request of the employee."
            ),
            _ => (
                "Certificate of Employment",
                $"This is to certify that Mr./Ms. <strong>{name}</strong> has been employed by <strong>{company}</strong> since <strong>{startDate}</strong>. He/She currently holds the position of <strong>{title ?? "Employee"}</strong>.",
                "This certificate is issued at the request of the employee."
            )
        };
    }
}
