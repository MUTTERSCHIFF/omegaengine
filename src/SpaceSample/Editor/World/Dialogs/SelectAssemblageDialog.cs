using System.Windows.Forms;
using AlphaEditor.Properties;
using Common.Collections;
using Common.Controls;
using World;

namespace AlphaEditor.World.Dialogs
{
    /// <summary>
    /// Dialog for selecting <see cref="Template{T}"/>es (with a preview pane).
    /// </summary>
    /// <typeparam name="T">The type of <see cref="Template{T}"/>es to select.</typeparam>
    /// <remarks>This dialog is a modal dialog.Communication is handled via the <see cref="DialogResult"/> and properties (<see cref="SelectedTemplate"/>)</remarks>
    public sealed class SelectTemplateDialog<T> : OKCancelDialog where T : Template<T>
    {
        #region Variables
        // Don't use WinForms designer for this, since it doesn't understand generics
        private readonly FilteredTreeView<T> _templateList = new FilteredTreeView<T>
        {
            Location = new System.Drawing.Point(12, 12),
            Size = new System.Drawing.Size(262, 209),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            TabIndex = 1
        };
        #endregion

        #region Properties
        /// <summary>
        /// The name of the <see cref="Template{T}"/> the user selected; <see langword="null"/> if none.
        /// </summary>
        public string SelectedTemplate { get { return (_templateList.SelectedEntry == null ? null : _templateList.SelectedEntry.Name); } }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new <see cref="Template{T}"/> selection dialog
        /// </summary>
        /// <param name="templates">The list of <see cref="Template{T}"/>es to choose from</param>
        public SelectTemplateDialog(INamedCollection<T> templates)
        {
            Text = Resources.TemplateSelection;
            _templateList.SelectionConfirmed += delegate
            {
                DialogResult = DialogResult.OK;
                OnOKClicked();
                Close();
            };
            Controls.Add(_templateList);

            _templateList.Entries = templates;
        }
        #endregion
    }
}
