#!/usr/bin/env python3
"""Poor man's WPF markup check for Linux: generates partial-class stubs (named fields + InitializeComponent)
from XAML so the C# of the app can be type-checked without the WindowsDesktop SDK, and reports XAML references
that have no target (event handlers, StaticResource keys)."""
import re, sys, os, glob
import xml.etree.ElementTree as ET
HERE=os.path.dirname(os.path.abspath(__file__))
APP=os.path.normpath(os.path.join(HERE,'..','..','src','PokeCorner.App'))
OUT=os.path.join(HERE,'stubs')
os.makedirs(OUT,exist_ok=True)
for f in glob.glob(OUT+'/*.cs'): os.remove(f)
X='{http://schemas.microsoft.com/winfx/2006/xaml}'
P='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
NS={'Rectangle':'System.Windows.Shapes','Ellipse':'System.Windows.Shapes','Path':'System.Windows.Shapes','Line':'System.Windows.Shapes','Polygon':'System.Windows.Shapes','Polyline':'System.Windows.Shapes',
 'UniformGrid':'System.Windows.Controls.Primitives','ToggleButton':'System.Windows.Controls.Primitives','Popup':'System.Windows.Controls.Primitives','ScrollBar':'System.Windows.Controls.Primitives','Thumb':'System.Windows.Controls.Primitives','RepeatButton':'System.Windows.Controls.Primitives','StatusBar':'System.Windows.Controls.Primitives','TabPanel':'System.Windows.Controls.Primitives','Track':'System.Windows.Controls.Primitives',
 'Run':'System.Windows.Documents','Hyperlink':'System.Windows.Documents','Span':'System.Windows.Documents','LineBreak':'System.Windows.Documents','Bold':'System.Windows.Documents',
 'Window':'System.Windows','Application':'System.Windows','ResourceDictionary':'System.Windows','Style':'System.Windows',
 'ScaleTransform':'System.Windows.Media','TranslateTransform':'System.Windows.Media','RotateTransform':'System.Windows.Media','TransformGroup':'System.Windows.Media','SolidColorBrush':'System.Windows.Media','LinearGradientBrush':'System.Windows.Media','RadialGradientBrush':'System.Windows.Media','GradientStop':'System.Windows.Media','ImageBrush':'System.Windows.Media','RectangleGeometry':'System.Windows.Media','SkewTransform':'System.Windows.Media',
 'DropShadowEffect':'System.Windows.Media.Effects','BlurEffect':'System.Windows.Media.Effects',
 'Storyboard':'System.Windows.Media.Animation','DoubleAnimation':'System.Windows.Media.Animation',
 'ColumnDefinition':'System.Windows.Controls','RowDefinition':'System.Windows.Controls'}
TEMPLATES={'ControlTemplate','DataTemplate','ItemsPanelTemplate','HierarchicalDataTemplate','Style'}
EVENTS=re.compile(r'^(Click|Checked|Unchecked|SelectionChanged|TextChanged|ValueChanged|Loaded|Unloaded|SizeChanged|Closed|Closing|KeyDown|KeyUp|PreviewKeyDown|PreviewKeyUp|MouseLeftButtonDown|MouseLeftButtonUp|MouseDown|MouseUp|MouseEnter|MouseLeave|MouseMove|MouseWheel|PreviewMouseWheel|PreviewMouseLeftButtonDown|PreviewMouseDown|GotFocus|LostFocus|DropDownOpened|DropDownClosed|StateChanged|Deactivated|Activated|IsVisibleChanged|ScrollChanged|RequestBringIntoView|PreviewTextInput|ContextMenuOpening|SourceInitialized|ContentRendered|LocationChanged|DragOver|Drop|ToolTipOpening|LostKeyboardFocus|GotKeyboardFocus|TargetUpdated|Completed)$')
problems=[]
def keys_of(path):
    txt=open(path,encoding='utf-8-sig').read()
    return set(re.findall(r'x:Key="([^"]+)"',txt))
theme_keys=set()
for t in glob.glob(APP+'/Theme/*.xaml'): theme_keys|=keys_of(t)
theme_keys|=keys_of(APP+'/App.xaml')
for path in sorted(glob.glob(APP+'/**/*.xaml',recursive=True)):
    if '/obj/' in path or '/bin/' in path: continue
    txt=open(path,encoding='utf-8-sig').read()
    try: root=ET.fromstring(txt)
    except ET.ParseError as e: problems.append(f'{path}: XML {e}'); continue
    # namespace prefixes
    prefixes=dict(re.findall(r'xmlns:(\w+)="clr-namespace:([^";]+)',txt))
    uri2ns={f'clr-namespace:{v}':v for v in prefixes.values()}
    # static resources
    own=keys_of(path)
    merged=set()
    for src in re.findall(r'ResourceDictionary Source="([^"]+)"',txt):
        name=src.split('/')[-1]
        for t in glob.glob(APP+'/**/'+name,recursive=True): merged|=keys_of(t)
    is_theme='/Theme/' in path
    for key in set(re.findall(r'\{(?:StaticResource) ([^}\s]+)\}',txt)):
        if key in own or key in merged or (not is_theme and key in theme_keys) or (is_theme and key in theme_keys and os.path.basename(path)!='NightDiary.xaml'): continue
        problems.append(f'{os.path.relpath(path,APP)}: resource "{key}" not found')
    cls=root.get(X+'Class')
    if not cls: continue
    cs=path+'.cs'
    code=open(cs,encoding='utf-8-sig').read() if os.path.exists(cs) else ''
    fields=[]
    def tname(tag):
        if tag.startswith('{'):
            uri,local=tag[1:].split('}')
            if uri==P: return NS.get(local,'System.Windows.Controls')+'.'+local
            for k,v in uri2ns.items():
                if uri.startswith(k): return v+'.'+local
            return None
        return None
    def walk(el,in_template):
        local=el.tag.split('}')[-1]
        if '.' in local and not in_template: pass
        tmpl=in_template or local in TEMPLATES
        name=el.get(X+'Name') or el.get('Name')
        if name and not tmpl:
            t=tname(el.tag)
            if t: fields.append((el.get(X+'FieldModifier','internal'),t,name))
        if not tmpl:
            for a,v in el.attrib.items():
                if EVENTS.match(a.split('}')[-1].split('.')[-1]) and re.match(r'^[A-Za-z_]\w*$',v):
                    if not re.search(r'\b'+re.escape(v)+r'\s*\(',code): problems.append(f'{os.path.relpath(path,APP)}: handler {v} ({a}) not in code-behind')
        for ch in el: walk(ch,tmpl)
    walk(root,False)
    ns,_,name=cls.rpartition('.')
    base=tname(root.tag)
    seen=set(); body=[]
    for mod,t,n in fields:
        if n in seen: problems.append(f'{os.path.relpath(path,APP)}: duplicate name {n}'); continue
        seen.add(n); body.append(f'    {mod} global::{t} {n} = null!;')
    main='    [System.STAThread] public static void Main() { }\n' if base=='System.Windows.Application' else ''
    open(os.path.join(OUT,cls+'.g.cs'),'w').write(f'#nullable enable\n#pragma warning disable\nnamespace {ns};\npublic partial class {name} : global::{base}\n{{\n'+'\n'.join(body)+f'\n    public void InitializeComponent() {{ }}\n{main}}}\n')
print('\n'.join(problems) if problems else 'xaml references ok')
