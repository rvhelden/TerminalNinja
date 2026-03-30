namespace TerminalNinja.Primitives;

/// <summary>
/// Named Nerd Font symbols mapped to their Unicode codepoints.
/// These icons require a Nerd Font-patched terminal font to display correctly.
/// <para>
/// Organized by icon set. Codepoint ranges:
/// <list type="bullet">
///   <item>Powerline: U+E0A0–U+E0D4</item>
///   <item>Pomicons: U+E000–U+E00A</item>
///   <item>Seti-UI + Custom: U+E5FA–U+E6AC</item>
///   <item>Devicons: U+E700–U+E7C5</item>
///   <item>Font Awesome: U+F000–U+F2E0</item>
///   <item>Font Awesome Extension: U+E200–U+E2A9</item>
///   <item>Weather Icons: U+E300–U+E3E3</item>
///   <item>Font Logos: U+F300–U+F372</item>
///   <item>Octicons: U+F400–U+F532</item>
///   <item>Codicons: U+EA60–U+EBE7</item>
/// </list>
/// </para>
/// </summary>
public enum Symbol : ushort
{
    // ─── None ────────────────────────────────────────────────────────
    /// <summary>No symbol (renders nothing).</summary>
    None = 0,

    // ─── Powerline ───────────────────────────────────────────────────
    /// <summary>Powerline branch icon (git branch). U+E0A0</summary>
    Branch = 0xE0A0,
    /// <summary>Powerline read-only (lock). U+E0A2</summary>
    ReadOnly = 0xE0A2,
    /// <summary>Powerline right arrow separator. U+E0B0</summary>
    ArrowRight = 0xE0B0,
    /// <summary>Powerline right arrow thin separator. U+E0B1</summary>
    ArrowRightThin = 0xE0B1,
    /// <summary>Powerline left arrow separator. U+E0B2</summary>
    ArrowLeft = 0xE0B2,
    /// <summary>Powerline left arrow thin separator. U+E0B3</summary>
    ArrowLeftThin = 0xE0B3,

    // ─── Seti-UI / Custom ────────────────────────────────────────────
    /// <summary>Folder icon. U+E5FF</summary>
    Folder = 0xE5FF,
    /// <summary>Folder open icon. U+E5FE</summary>
    FolderOpen = 0xE5FE,
    /// <summary>File icon (generic). U+E60E</summary>
    File = 0xE60E,
    /// <summary>C# file icon. U+E648</summary>
    FileCSharp = 0xE648,
    /// <summary>JSON file icon. U+E60B</summary>
    FileJson = 0xE60B,
    /// <summary>XML file icon. U+E619</summary>
    FileXml = 0xE619,
    /// <summary>Config/settings file icon. U+E615</summary>
    FileConfig = 0xE615,
    /// <summary>Image file icon. U+E60D</summary>
    FileImage = 0xE60D,
    /// <summary>Markdown file icon. U+E609</summary>
    FileMarkdown = 0xE609,
    /// <summary>TypeScript file icon. U+E628</summary>
    FileTypeScript = 0xE628,
    /// <summary>JavaScript file icon. U+E60C</summary>
    FileJavaScript = 0xE60C,
    /// <summary>CSS file icon. U+E614</summary>
    FileCss = 0xE614,
    /// <summary>HTML file icon. U+E60E</summary>
    FileHtml = 0xE636,
    /// <summary>Python file icon. U+E606</summary>
    FilePython = 0xE606,

    // ─── Devicons ────────────────────────────────────────────────────
    /// <summary>Git icon. U+E702</summary>
    Git = 0xE702,
    /// <summary>Git branch icon (devicon). U+E725</summary>
    GitBranch = 0xE725,
    /// <summary>Git compare icon. U+E728</summary>
    GitCompare = 0xE728,
    /// <summary>Git merge icon. U+E727</summary>
    GitMerge = 0xE727,
    /// <summary>Terminal / console icon. U+E795</summary>
    Terminal = 0xE795,
    /// <summary>Code / brackets icon. U+E796</summary>
    Code = 0xE796,
    /// <summary>Database icon. U+E706</summary>
    Database = 0xE706,
    /// <summary>Cloud icon. U+E707</summary>
    Cloud = 0xE707,
    /// <summary>.NET icon. U+E77F</summary>
    DotNet = 0xE77F,
    /// <summary>Docker icon. U+E7B0</summary>
    Docker = 0xE7B0,
    /// <summary>NPM icon. U+E71E</summary>
    Npm = 0xE71E,
    /// <summary>React icon. U+E7BA</summary>
    React = 0xE7BA,
    /// <summary>Rust icon. U+E7A8</summary>
    Rust = 0xE7A8,
    /// <summary>Go (golang) icon. U+E724</summary>
    Go = 0xE724,
    /// <summary>Java icon. U+E738</summary>
    Java = 0xE738,
    /// <summary>Ruby icon. U+E739</summary>
    Ruby = 0xE739,

    // ─── Font Awesome ────────────────────────────────────────────────
    /// <summary>Heart icon. U+F004</summary>
    Heart = 0xF004,
    /// <summary>Star icon (filled). U+F005</summary>
    Star = 0xF005,
    /// <summary>Star icon (outline). U+F006</summary>
    StarOutline = 0xF006,
    /// <summary>User/person icon. U+F007</summary>
    User = 0xF007,
    /// <summary>Film / video icon. U+F008</summary>
    Film = 0xF008,
    /// <summary>Check / checkmark icon. U+F00C</summary>
    Check = 0xF00C,
    /// <summary>X / close icon. U+F00D</summary>
    Close = 0xF00D,
    /// <summary>Search / magnifier icon. U+F002</summary>
    Search = 0xF002,
    /// <summary>Cog / gear icon. U+F013</summary>
    Settings = 0xF013,
    /// <summary>Home icon. U+F015</summary>
    Home = 0xF015,
    /// <summary>Clock / time icon. U+F017</summary>
    Clock = 0xF017,
    /// <summary>Download icon. U+F019</summary>
    Download = 0xF019,
    /// <summary>Upload icon. U+F093</summary>
    Upload = 0xF093,
    /// <summary>Inbox icon. U+F01C</summary>
    Inbox = 0xF01C,
    /// <summary>Refresh / sync icon. U+F021</summary>
    Refresh = 0xF021,
    /// <summary>Lock icon. U+F023</summary>
    Lock = 0xF023,
    /// <summary>Flag icon. U+F024</summary>
    Flag = 0xF024,
    /// <summary>Bookmark icon. U+F02E</summary>
    Bookmark = 0xF02E,
    /// <summary>Print icon. U+F02F</summary>
    Print = 0xF02F,
    /// <summary>Camera icon. U+F030</summary>
    Camera = 0xF030,
    /// <summary>List / menu icon. U+F0C9</summary>
    Menu = 0xF0C9,
    /// <summary>Table / grid icon. U+F0CE</summary>
    Table = 0xF0CE,
    /// <summary>Magic wand icon. U+F0D0</summary>
    Magic = 0xF0D0,
    /// <summary>Envelope / mail icon. U+F0E0</summary>
    Mail = 0xF0E0,
    /// <summary>Pencil / edit icon. U+F040</summary>
    Edit = 0xF040,
    /// <summary>Trash / delete icon. U+F1F8</summary>
    Trash = 0xF1F8,
    /// <summary>Eye / visible icon. U+F06E</summary>
    Eye = 0xF06E,
    /// <summary>Eye-slash / hidden icon. U+F070</summary>
    EyeOff = 0xF070,
    /// <summary>Warning triangle icon. U+F071</summary>
    Warning = 0xF071,
    /// <summary>Calendar icon. U+F073</summary>
    Calendar = 0xF073,
    /// <summary>Comment / chat icon. U+F075</summary>
    Comment = 0xF075,
    /// <summary>Shopping cart icon. U+F07A</summary>
    Cart = 0xF07A,
    /// <summary>Folder icon (FA). U+F07B</summary>
    FolderFA = 0xF07B,
    /// <summary>Folder open icon (FA). U+F07C</summary>
    FolderOpenFA = 0xF07C,
    /// <summary>Key icon. U+F084</summary>
    Key = 0xF084,
    /// <summary>Cogs / settings icon. U+F085</summary>
    Cogs = 0xF085,
    /// <summary>Link / chain icon. U+F0C1</summary>
    Link = 0xF0C1,
    /// <summary>Paperclip / attach icon. U+F0C6</summary>
    Attach = 0xF0C6,
    /// <summary>Bolt / lightning icon. U+F0E7</summary>
    Bolt = 0xF0E7,
    /// <summary>Bell / notification icon. U+F0F3</summary>
    Bell = 0xF0F3,
    /// <summary>Bug icon. U+F188</summary>
    Bug = 0xF188,
    /// <summary>Circle icon (filled). U+F111</summary>
    Circle = 0xF111,
    /// <summary>Circle icon (outline). U+F10C</summary>
    CircleOutline = 0xF10C,
    /// <summary>Square icon (outline). U+F096</summary>
    SquareOutline = 0xF096,
    /// <summary>Check square icon. U+F14A</summary>
    CheckSquare = 0xF14A,
    /// <summary>Plus / add icon. U+F067</summary>
    Plus = 0xF067,
    /// <summary>Minus / remove icon. U+F068</summary>
    Minus = 0xF068,
    /// <summary>Info circle icon. U+F05A</summary>
    Info = 0xF05A,
    /// <summary>Question circle icon. U+F059</summary>
    Question = 0xF059,
    /// <summary>Exclamation circle icon. U+F06A</summary>
    Error = 0xF06A,
    /// <summary>Spinner icon. U+F110</summary>
    Spinner = 0xF110,
    /// <summary>WiFi icon. U+F1EB</summary>
    Wifi = 0xF1EB,
    /// <summary>Battery full icon. U+F240</summary>
    BatteryFull = 0xF240,
    /// <summary>Battery half icon. U+F242</summary>
    BatteryHalf = 0xF242,
    /// <summary>Battery empty icon. U+F244</summary>
    BatteryEmpty = 0xF244,

    // ─── Arrows (Font Awesome) ───────────────────────────────────────
    /// <summary>Chevron up icon. U+F077</summary>
    ChevronUp = 0xF077,
    /// <summary>Chevron down icon. U+F078</summary>
    ChevronDown = 0xF078,
    /// <summary>Chevron left icon. U+F053</summary>
    ChevronLeft = 0xF053,
    /// <summary>Chevron right icon. U+F054</summary>
    ChevronRight = 0xF054,
    /// <summary>Arrow up icon. U+F062</summary>
    ArrowUp = 0xF062,
    /// <summary>Arrow down icon. U+F063</summary>
    ArrowDown = 0xF063,

    // ─── Media (Font Awesome) ────────────────────────────────────────
    /// <summary>Play icon. U+F04B</summary>
    Play = 0xF04B,
    /// <summary>Pause icon. U+F04C</summary>
    Pause = 0xF04C,
    /// <summary>Stop icon. U+F04D</summary>
    Stop = 0xF04D,
    /// <summary>Forward icon. U+F04E</summary>
    Forward = 0xF04E,
    /// <summary>Backward icon. U+F04A</summary>
    Backward = 0xF04A,
    /// <summary>Volume up icon. U+F028</summary>
    VolumeUp = 0xF028,
    /// <summary>Volume off / mute icon. U+F026</summary>
    VolumeOff = 0xF026,
    /// <summary>Music icon. U+F001</summary>
    Music = 0xF001,

    // ─── Font Logos ──────────────────────────────────────────────────
    /// <summary>Linux icon. U+F31A</summary>
    Linux = 0xF31A,
    /// <summary>Windows icon. U+F17A</summary>
    Windows = 0xF17A,
    /// <summary>Apple icon. U+F179</summary>
    Apple = 0xF179,
    /// <summary>Ubuntu icon. U+F31B</summary>
    Ubuntu = 0xF31B,
    /// <summary>Fedora icon. U+F30A</summary>
    Fedora = 0xF30A,
    /// <summary>Arch Linux icon. U+F303</summary>
    ArchLinux = 0xF303,
    /// <summary>Debian icon. U+F306</summary>
    Debian = 0xF306,

    // ─── Octicons ────────────────────────────────────────────────────
    /// <summary>Octicons git commit icon. U+F417</summary>
    GitCommit = 0xF417,
    /// <summary>Octicons git pull request icon. U+F407</summary>
    GitPullRequest = 0xF407,
    /// <summary>Octicons issue opened icon. U+F41B</summary>
    IssueOpened = 0xF41B,
    /// <summary>Octicons issue closed icon. U+F41C</summary>
    IssueClosed = 0xF41C,
    /// <summary>Octicons repo icon. U+F401</summary>
    Repo = 0xF401,
    /// <summary>Octicons package icon. U+F487</summary>
    Package = 0xF487,
    /// <summary>Octicons tag icon. U+F412</summary>
    Tag = 0xF412,
    /// <summary>Octicons rocket icon. U+F427</summary>
    Rocket = 0xF427,
    /// <summary>Octicons shield icon. U+F49E</summary>
    Shield = 0xF49E,
    /// <summary>Octicons tools icon. U+F425</summary>
    Tools = 0xF425,
    /// <summary>Octicons terminal icon. U+F489</summary>
    TerminalOct = 0xF489,

    // ─── Codicons ────────────────────────────────────────────────────
    /// <summary>Codicon debug icon. U+EA87</summary>
    Debug = 0xEA87,
    /// <summary>Codicon extensions icon. U+EA78</summary>
    Extensions = 0xEA78,
    /// <summary>Codicon source control icon. U+EA68</summary>
    SourceControl = 0xEA68,
    /// <summary>Codicon run / play icon. U+EB2C</summary>
    Run = 0xEB2C,
    /// <summary>Codicon save icon. U+EB4B</summary>
    Save = 0xEB4B,
    /// <summary>Codicon new file icon. U+EA7F</summary>
    NewFile = 0xEA7F,
    /// <summary>Codicon new folder icon. U+EA80</summary>
    NewFolder = 0xEA80,
    /// <summary>Codicon loading / sync icon. U+EB29</summary>
    Loading = 0xEB29,
    /// <summary>Codicon account icon. U+EB99</summary>
    Account = 0xEB99,
    /// <summary>Codicon archive icon. U+EABC</summary>
    Archive = 0xEABC,
    /// <summary>Codicon beaker / test icon. U+EA79</summary>
    Beaker = 0xEA79,
    /// <summary>Codicon book icon. U+EA9C</summary>
    Book = 0xEA9C,
    /// <summary>Codicon copy icon. U+EAB1</summary>
    Copy = 0xEAB1,
    /// <summary>Codicon dashboard icon. U+EA90</summary>
    Dashboard = 0xEA90,
    /// <summary>Codicon diff icon. U+EA8C</summary>
    Diff = 0xEA8C,
    /// <summary>Codicon export icon. U+EADF</summary>
    Export = 0xEADF,
    /// <summary>Codicon filter icon. U+EA76</summary>
    Filter = 0xEA76,
    /// <summary>Codicon flame / fire icon. U+EB17</summary>
    Flame = 0xEB17,
    /// <summary>Codicon graph icon. U+EA94</summary>
    Graph = 0xEA94,
    /// <summary>Codicon history icon. U+EA82</summary>
    History = 0xEA82,
    /// <summary>Codicon lightbulb icon. U+EA61</summary>
    Lightbulb = 0xEA61,
    /// <summary>Codicon milestone icon. U+EA97</summary>
    Milestone = 0xEA97,
    /// <summary>Codicon pin icon. U+EB3E</summary>
    Pin = 0xEB3E,
    /// <summary>Codicon remote icon. U+EB39</summary>
    Remote = 0xEB39,
    /// <summary>Codicon server icon. U+EABC</summary>
    Server = 0xEAA1,
    /// <summary>Codicon symbol-event icon. U+EA86</summary>
    Event = 0xEA86,
    /// <summary>Codicon target icon. U+EB44</summary>
    Target = 0xEB44,
    /// <summary>Codicon workspace icon. U+EAE7</summary>
    Workspace = 0xEAE7,
    /// <summary>Codicon zap icon. U+EA96</summary>
    Zap = 0xEA96,

    // ─── IEC Power Symbols ───────────────────────────────────────────
    /// <summary>Power icon. U+23FB</summary>
    Power = 0x23FB,
    /// <summary>Power on icon. U+23FD</summary>
    PowerOn = 0x23FD,
    /// <summary>Sleep icon. U+23FE</summary>
    Sleep = 0x23FE,

    // ─── Weather Icons ───────────────────────────────────────────────
    /// <summary>Day sunny icon. U+E302</summary>
    Sunny = 0xE302,
    /// <summary>Night clear icon. U+E32B</summary>
    Moon = 0xE32B,
    /// <summary>Cloud icon (weather). U+E33D</summary>
    CloudWeather = 0xE33D,
    /// <summary>Rain icon. U+E318</summary>
    Rain = 0xE318,
    /// <summary>Snow icon. U+E31A</summary>
    Snow = 0xE31A,
    /// <summary>Thunderstorm icon. U+E31D</summary>
    Thunderstorm = 0xE31D,
    /// <summary>Fog icon. U+E313</summary>
    Fog = 0xE313,
    /// <summary>Thermometer icon. U+E350</summary>
    Thermometer = 0xE350,
}
