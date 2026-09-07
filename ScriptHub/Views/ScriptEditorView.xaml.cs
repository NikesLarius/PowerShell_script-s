using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Search;
using ScriptHub.Models;
using ScriptHub.ViewModels;

namespace ScriptHub.Views;

public partial class ScriptEditorView : UserControl
{
    private static IHighlightingDefinition? _psHighlighting;
    private static IHighlightingDefinition? _batchHighlighting;
    private bool _isUpdatingTextInternally;

    public ScriptEditorView()
    {
        InitializeComponent();
        InitializeHighlighting();
        SearchPanel.Install(CodeEditor);

        DataContextChanged += ScriptEditorView_DataContextChanged;
        CodeEditor.TextChanged += CodeEditor_TextChanged;
        Loaded += ScriptEditorView_Loaded;
    }

    private void InitializeHighlighting()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();

            using var psStream = assembly.GetManifestResourceStream("ScriptHub.Resources.Syntax.PowerShell.xshd");
            if (psStream != null)
            {
                using var reader = new XmlTextReader(psStream);
                _psHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }

            using var batStream = assembly.GetManifestResourceStream("ScriptHub.Resources.Syntax.Batch.xshd");
            if (batStream != null)
            {
                using var reader = new XmlTextReader(batStream);
                _batchHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
        }
        catch { }
    }

    private void ScriptEditorView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateEditorContentAndSyntax();
    }

    private void ScriptEditorView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ScriptEditorViewModel oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (e.NewValue is ScriptEditorViewModel newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
            UpdateEditorContentAndSyntax();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScriptEditorViewModel.CodeContent))
        {
            if (!_isUpdatingTextInternally && DataContext is ScriptEditorViewModel vm)
            {
                if (CodeEditor.Text != vm.CodeContent)
                {
                    CodeEditor.Text = vm.CodeContent ?? "";
                }
            }
        }
        else if (e.PropertyName == nameof(ScriptEditorViewModel.ScriptType))
        {
            UpdateSyntaxHighlighting();
        }
    }

    private void CodeEditor_TextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ScriptEditorViewModel vm)
        {
            _isUpdatingTextInternally = true;
            vm.CodeContent = CodeEditor.Text;
            _isUpdatingTextInternally = false;
        }
    }

    private void UpdateEditorContentAndSyntax()
    {
        if (DataContext is ScriptEditorViewModel vm)
        {
            _isUpdatingTextInternally = true;
            CodeEditor.Text = vm.CodeContent ?? "";
            _isUpdatingTextInternally = false;

            UpdateSyntaxHighlighting();
        }
    }

    private void UpdateSyntaxHighlighting()
    {
        if (DataContext is ScriptEditorViewModel vm)
        {
            CodeEditor.SyntaxHighlighting = vm.ScriptType == ScriptType.PowerShell
                ? _psHighlighting
                : _batchHighlighting;
        }
    }
}
