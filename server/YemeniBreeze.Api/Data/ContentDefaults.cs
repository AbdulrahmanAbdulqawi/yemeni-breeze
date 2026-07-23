namespace YemeniBreeze.Api.Data;

/// <summary>
/// Default copy for every admin-editable content block, keyed the same way as the
/// client's transloco i18n keys. Seeded once at startup (see SeedData.RunAsync) —
/// adding a new editable field later only needs a new entry here, no migration.
/// </summary>
public static class ContentDefaults
{
    public static readonly Dictionary<string, (string En, string Nl, string Ar)> Values = new()
    {
        ["home.heroTitle"] = ("Yemen is more than a war", "Jemen is meer dan een oorlog", "اليمن أكثر من مجرد حرب"),
        ["home.heroSubtitle"] = (
            "We are a youth-led cultural initiative in Amsterdam telling Yemen's story ourselves — through food, music, art, and genuine human connection.",
            "Wij zijn een cultureel jongereninitiatief in Amsterdam dat het verhaal van Jemen zelf vertelt — via eten, muziek, kunst en echte menselijke verbinding.",
            "نحن مبادرة ثقافية شبابية في أمستردام نروي قصة اليمن بأنفسنا — عبر الطعام والموسيقى والفن والتواصل الإنساني الحقيقي."),
        ["home.heroCta"] = ("Join our next event", "Doe mee met ons volgende evenement", "انضم إلى فعاليتنا القادمة"),
        ["home.heroSecondary"] = ("Read our story", "Lees ons verhaal", "اقرأ قصتنا"),
        ["home.connect"] = ("We CONNECT", "Wij VERBINDEN", "نَصِلُ"),
        ["home.connectText"] = (
            "cultures — bringing Dutch locals, refugees, and the Yemeni diaspora to one table.",
            "culturen — Nederlanders, vluchtelingen en de Jemenitische diaspora aan één tafel.",
            "الثقافات — نجمع الهولنديين واللاجئين والجالية اليمنية على طاولة واحدة."),
        ["home.showcase"] = ("We SHOWCASE", "Wij TONEN", "نُظهِرُ"),
        ["home.showcaseText"] = (
            "Yemen — its civilization, cuisine, music, and a people full of warmth and pride.",
            "Jemen — zijn beschaving, keuken, muziek en een volk vol warmte en trots.",
            "اليمن — حضارته ومطبخه وموسيقاه وشعبه المليء بالدفء والفخر."),
        ["home.build"] = ("We BUILD", "Wij BOUWEN", "نَبني"),
        ["home.buildText"] = (
            "bridges — through shared experience, not charity. Connection as equals.",
            "bruggen — via gedeelde ervaring, geen liefdadigheid. Verbinding als gelijken.",
            "الجسور — عبر التجربة المشتركة لا الإحسان. تواصلٌ بين أنداد."),
        ["home.statsTitle"] = ("Our first year", "Ons eerste jaar", "عامنا الأول"),
        ["home.statAttendees"] = ("Guests in person", "Gasten ter plaatse", "ضيف حضروا فعالياتنا"),
        ["home.statAttendeesValue"] = ("650+", "650+", "+650"),
        ["home.statViews"] = ("Views online", "Views online", "مشاهدة عبر الإنترنت"),
        ["home.statViewsValue"] = ("600K+", "600K+", "+600 ألف"),
        ["home.statEvents"] = ("Major events", "Grote evenementen", "فعاليات كبرى"),
        ["home.statEventsValue"] = ("3", "3", "3"),
        ["home.statMeals"] = ("Meals delivered to camps", "Maaltijden bezorgd bij AZC's", "وجبة وصلت إلى المخيمات"),
        ["home.statMealsValue"] = ("350", "350", "350"),
        ["home.upcomingTitle"] = ("Upcoming event", "Aankomend evenement", "الفعالية القادمة"),
        ["home.missionTitle"] = ("Why we exist", "Waarom wij bestaan", "لماذا وُجدنا"),
        ["home.missionText"] = (
            "Most people outside Yemen only know the war. We show another side: history, art, beauty. Yemeni Breeze celebrates what Yemen has given to the world — and challenges the stereotypes and discrimination that Yemenis and other diaspora communities face.",
            "De meeste mensen buiten Jemen kennen alleen de oorlog. Wij laten een andere kant zien: geschiedenis, kunst, schoonheid. Yemeni Breeze viert wat Jemen de wereld heeft gegeven — en bestrijdt de stereotypen en discriminatie waar Jemenieten en andere diasporagemeenschappen mee te maken hebben.",
            "معظم الناس خارج اليمن لا يعرفون سوى الحرب. نحن نُظهر وجهًا آخر: التاريخ والفن والجمال. تحتفي نسمات اليمن بما قدّمه اليمن للعالم — وتتصدى للصور النمطية والتمييز الذي يواجهه اليمنيون وجاليات المهجر الأخرى."),

        ["about.title"] = ("Our Story", "Ons verhaal", "قصتنا"),
        ["about.intro"] = (
            "Yemeni Breeze is a youth-led cultural initiative based in Amsterdam, built on one powerful belief: Yemen is more than a war. Yemen has a civilization thousands of years old, a rich culinary tradition, powerful music, and a people full of warmth, creativity, and pride.",
            "Yemeni Breeze is een cultureel jongereninitiatief in Amsterdam, gebouwd op één krachtige overtuiging: Jemen is meer dan een oorlog. Jemen heeft een duizenden jaren oude beschaving, een rijke culinaire traditie, krachtige muziek en een volk vol warmte, creativiteit en trots.",
            "نسمات اليمن مبادرة ثقافية شبابية مقرها أمستردام، بُنيت على قناعة راسخة: اليمن أكثر من مجرد حرب. لليمن حضارة عمرها آلاف السنين، وتقاليد طهي غنية، وموسيقى مؤثرة، وشعب مفعم بالدفء والإبداع والفخر."),
        ["about.founding"] = (
            "Yemeni Breeze was founded by Koutaiba, a young Yemeni in Amsterdam who carried a vision: to bring Yemeni culture to the world in a way that is authentic, modern, and impactful. His close friend Ali joined from the very beginning, and together they built the foundation of what has become a growing movement.",
            "Yemeni Breeze is opgericht door Koutaiba, een jonge Jemeniet in Amsterdam met een visie: de Jemenitische cultuur op een authentieke, moderne en impactvolle manier naar de wereld brengen. Zijn goede vriend Ali sloot zich vanaf het allereerste begin aan, en samen legden zij het fundament van wat inmiddels een groeiende beweging is.",
            "أسّس نسمات اليمن قتيبة، شاب يمني في أمستردام حمل رؤية: تقديم الثقافة اليمنية للعالم بطريقة أصيلة وعصرية ومؤثرة. انضم إليه صديقه المقرب علي منذ البداية، وبنيا معًا أساس ما أصبح اليوم حركة متنامية."),
        ["about.open"] = (
            "Today, Yemeni Breeze is an open community. Any Yemeni in the Netherlands — or anyone who believes in our mission — is welcome to join the team, contribute ideas, and help shape what we become. This movement does not belong to a few people. It belongs to the Yemeni identity itself.",
            "Vandaag is Yemeni Breeze een open community. Elke Jemeniet in Nederland — of iedereen die in onze missie gelooft — is welkom om mee te doen, ideeën aan te dragen en mede vorm te geven aan wat we worden. Deze beweging is niet van een paar mensen. Ze is van de Jemenitische identiteit zelf.",
            "اليوم، نسمات اليمن مجتمع مفتوح. كل يمني في هولندا — وكل من يؤمن برسالتنا — مرحب به للانضمام إلى الفريق والمساهمة بالأفكار والمشاركة في صياغة مستقبلنا. هذه الحركة لا تخص أفرادًا بعينهم، بل تنتمي إلى الهوية اليمنية ذاتها."),
        ["about.missionTitle"] = ("Our Mission", "Onze missie", "رسالتنا"),
        ["about.missionText"] = (
            "To connect Yemeni culture with the world: changing the global image of Yemen, celebrating its rich heritage in all its forms, and addressing real social issues facing diaspora communities in the Netherlands, including racial discrimination and the marginalization of refugees.",
            "De Jemenitische cultuur verbinden met de wereld: het wereldbeeld van Jemen veranderen, zijn rijke erfgoed in al zijn vormen vieren, en echte maatschappelijke problemen aanpakken waar diasporagemeenschappen in Nederland mee te maken hebben, waaronder racisme en de marginalisering van vluchtelingen.",
            "وصل الثقافة اليمنية بالعالم: تغيير الصورة العالمية عن اليمن، والاحتفاء بتراثه الغني بكل أشكاله، ومعالجة قضايا اجتماعية حقيقية تواجه جاليات المهجر في هولندا، بما في ذلك التمييز العنصري وتهميش اللاجئين."),
        ["about.valuesTitle"] = ("Our Values", "Onze waarden", "قيمنا"),
        ["about.valuePride"] = ("Pride", "Trots", "الفخر"),
        ["about.valuePrideText"] = (
            "We carry Yemeni identity with confidence and love.",
            "Wij dragen de Jemenitische identiteit met zelfvertrouwen en liefde.",
            "نحمل الهوية اليمنية بثقة ومحبة."),
        ["about.valueOpenness"] = ("Openness", "Openheid", "الانفتاح"),
        ["about.valueOpennessText"] = (
            "Our doors are open to every person, every background, every nationality.",
            "Onze deuren staan open voor ieder persoon, elke achtergrond, elke nationaliteit.",
            "أبوابنا مفتوحة لكل إنسان، من كل خلفية وكل جنسية."),
        ["about.valueService"] = ("Service", "Dienstbaarheid", "العطاء"),
        ["about.valueServiceText"] = (
            "We give back to the communities around us.",
            "Wij geven terug aan de gemeenschappen om ons heen.",
            "نردّ الجميل للمجتمعات من حولنا."),
        ["about.valueExcellence"] = ("Excellence", "Excellentie", "الإتقان"),
        ["about.valueExcellenceText"] = (
            "Every detail matters — from the food to the music to the welcome.",
            "Elk detail telt — van het eten tot de muziek tot het welkom.",
            "كل تفصيلة مهمة — من الطعام إلى الموسيقى إلى حفاوة الاستقبال."),
        ["about.valueUnity"] = ("Unity", "Eenheid", "الوحدة"),
        ["about.valueUnityText"] = (
            "Yemen's story belongs to all of us.",
            "Het verhaal van Jemen is van ons allemaal.",
            "قصة اليمن ملك لنا جميعًا."),
        ["about.socialTitle"] = ("Our Social Mission", "Onze maatschappelijke missie", "رسالتنا الاجتماعية"),
        ["about.socialText"] = (
            "We believe the most powerful response to hatred is not argument — it is experience. When a Dutch person has cooked Yemeni food with their own hands, written a message of love to a stranger in a camp, heard the oud played, and shared coffee at the same table — they do not see refugees the same way anymore. That is our method. And it works.",
            "Wij geloven dat het krachtigste antwoord op haat geen discussie is — maar ervaring. Als een Nederlander met eigen handen Jemenitisch eten heeft gekookt, een liefdevolle boodschap heeft geschreven aan een vreemde in een AZC, de oud heeft horen spelen en koffie heeft gedeeld aan dezelfde tafel — dan kijkt diegene nooit meer op dezelfde manier naar vluchtelingen. Dat is onze methode. En die werkt.",
            "نؤمن أن أقوى ردّ على الكراهية ليس الجدال — بل التجربة. عندما يطبخ هولندي الطعام اليمني بيديه، ويكتب رسالة محبة لغريب في مخيم، ويسمع عزف العود، ويتشارك القهوة على الطاولة نفسها — لن ينظر إلى اللاجئين بالطريقة ذاتها بعد ذلك. هذه طريقتنا. وهي تنجح."),
        ["about.teamTitle"] = ("The Team", "Het team", "الفريق"),
        ["about.teamNote"] = (
            "We are actively growing. If you are Yemeni and living in the Netherlands, or if you share our vision, we welcome you to reach out and become part of Yemeni Breeze.",
            "We groeien actief. Ben je Jemeniet en woon je in Nederland, of deel je onze visie? Neem contact op en word onderdeel van Yemeni Breeze.",
            "نحن ننمو باستمرار. إذا كنت يمنيًا تعيش في هولندا، أو كنت تشاركنا رؤيتنا، نرحب بتواصلك وانضمامك إلى نسمات اليمن."),
        ["about.joinCta"] = ("Join the movement", "Sluit je aan", "انضم إلى الحركة"),

        ["events.intro"] = (
            "Communal dinners, cooking workshops, and cultural gatherings in Amsterdam — join us at the table.",
            "Gezamenlijke diners, kookworkshops en culturele bijeenkomsten in Amsterdam — schuif bij ons aan tafel.",
            "عشاءات جماعية وورش طبخ ولقاءات ثقافية في أمستردام — انضم إلينا على الطاولة."),

        ["gallery.intro"] = (
            "Moments from our events in Amsterdam — hands in the kitchen, music in the room, and tables where Yemeni and Dutch guests sit together as equals.",
            "Momenten van onze evenementen in Amsterdam — handen in de keuken, muziek in de zaal, en tafels waar Jemenitische en Nederlandse gasten als gelijken samen zitten.",
            "لحظات من فعالياتنا في أمستردام — أيادٍ في المطبخ، وموسيقى تملأ المكان، وطاولات يجلس حولها الضيوف اليمنيون والهولنديون سواسية."),

        ["contact.intro"] = (
            "We welcome all inquiries, partnership proposals, and expressions of interest from individuals, organizations, and institutions who share our vision.",
            "We verwelkomen alle vragen, partnervoorstellen en interesse van personen, organisaties en instellingen die onze visie delen.",
            "نرحب بجميع الاستفسارات ومقترحات الشراكة واهتمام الأفراد والمنظمات والمؤسسات التي تشاركنا رؤيتنا."),
        ["contact.partnershipText"] = (
            "Yemeni Breeze is registering as a formal Stichting (foundation) under Dutch law. We partner with cultural institutions, municipalities, refugee support organizations, and AZC locations.",
            "Yemeni Breeze registreert zich als formele Stichting naar Nederlands recht. We werken samen met culturele instellingen, gemeenten, vluchtelingenorganisaties en AZC-locaties.",
            "نسمات اليمن في طور التسجيل كمؤسسة (Stichting) رسمية بموجب القانون الهولندي. نتعاون مع المؤسسات الثقافية والبلديات ومنظمات دعم اللاجئين ومراكز استقبال طالبي اللجوء."),
        ["contact.volunteerText"] = (
            "There is no gate. If you care about Yemen and want to do something meaningful, Yemeni Breeze is your platform too.",
            "Er is geen drempel. Geef je om Jemen en wil je iets betekenisvols doen? Dan is Yemeni Breeze ook jouw platform.",
            "لا توجد حواجز. إذا كنت تهتم باليمن وتريد أن تفعل شيئًا ذا معنى، فنسمات اليمن منصتك أيضًا."),

        ["footer.tagline"] = (
            "We CONNECT cultures. We SHOWCASE Yemen. We BUILD bridges.",
            "Wij VERBINDEN culturen. Wij TONEN Jemen. Wij BOUWEN bruggen.",
            "نَصِلُ الثقافات. نُظهِرُ اليمن. نَبني الجسور."),
        ["footer.location"] = ("Amsterdam, The Netherlands", "Amsterdam, Nederland", "أمستردام، هولندا"),
        ["footer.rights"] = (
            "Yemeni Breeze — an open movement for everyone who believes in the power of culture.",
            "Yemeni Breeze — een open beweging voor iedereen die gelooft in de kracht van cultuur.",
            "نسمات اليمن — حركة مفتوحة لكل من يؤمن بقوة الثقافة.")
    };
}
