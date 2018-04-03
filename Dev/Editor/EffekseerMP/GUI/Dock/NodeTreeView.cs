﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Effekseer.GUI.Dock
{
    class NodeTreeView : DockPanel
    {
        internal Utils.DelayedList<NodeTreeViewNode> Children = new Utils.DelayedList<NodeTreeViewNode>();

		internal List<IControl> menuItems = new List<IControl>();

		NodeTreeViewNode SelectedNode = null;

		bool isPopupShown = false;

		public NodeTreeView()
        {
			Label = Resources.GetString("NodeTree");

			Core.OnAfterNew += OnRenew;
			Core.OnAfterLoad += OnRenew;
			Core.OnAfterSelectNode += OnAfterSelect;

			Func<Func<bool>, Menu.MenuItem> create_menu_item_from_commands = (a) =>
			{
				var item = new Menu.MenuItem();
				var attributes = a.Method.GetCustomAttributes(false);
				var uniquename = UniqueNameAttribute.GetUniqueName(attributes);
				item.Label = NameAttribute.GetName(attributes);
				item.Shortcut = Shortcuts.GetShortcutText(uniquename);
				item.Clicked += () =>
				{
					a();
				};

				return item;
			};

			menuItems.Add(create_menu_item_from_commands(Commands.AddNode));
			menuItems.Add(create_menu_item_from_commands(Commands.InsertNode));
			menuItems.Add(create_menu_item_from_commands(Commands.RemoveNode));

			menuItems.Add(new Menu.MenuSeparator());

			menuItems.Add(create_menu_item_from_commands(Commands.Copy));
			menuItems.Add(create_menu_item_from_commands(Commands.Paste));
			menuItems.Add(create_menu_item_from_commands(Commands.PasteInfo));

			menuItems.Add(new Menu.MenuSeparator());

			menuItems.Add(create_menu_item_from_commands(Commands.Undo));
			menuItems.Add(create_menu_item_from_commands(Commands.Redo));

			Renew();
		}

		override protected void UpdateInternal()
		{
			isPopupShown = false;

			Children.Lock();
			foreach (var child in Children.Internal)
			{
				child.Update();
			}
			Children.Unlock();
		}

        /// <summary>
        /// Renew all view
        /// </summary>
        public void Renew()
        {
			// Reset all
			if (Children.Count != 1 || Children[0].node != Core.Root)
			{
				Children.Lock();
				foreach (var child in Children.Internal)
				{
					child.RemoveEvent(true);
				}
				Children.Unlock();
				Children.Clear();

				Children.Add(new NodeTreeViewNode(this, Core.Root));
			}

            Action<NodeTreeViewNode, Data.NodeBase> set_nodes = null;
            set_nodes = (treenode, node) =>
            {
                var tns = treenode.Children;
                var ns = node.Children;

                for (int ind = 0; ind < ns.Count;)
                {
                    // not need to change
                    if (ind < tns.Count && ((NodeTreeViewNode)tns[ind]).node == ns[ind])
                    {
                        ind++;
                        continue;
                    }

                    // need to change
                    while (tns.Count > ind)
                    {
                        ((NodeTreeViewNode)tns[ind]).RemoveEvent(true);
                        tns.RemoveAt(ind);
                    }

                    while (ns.Count > ind)
                    {
                        tns.Add(new NodeTreeViewNode(this, ns[ind]));
                        ind++;
                    }
                }

                for (int i = 0; i < ns.Count; i++)
                {
                    set_nodes(tns[i], ns[i]);
                }
            };

            set_nodes(Children[0], Core.Root);

            ExpandAll();
        }

        public void ExpandAll()
        {
            Console.WriteLine("Not implemented");
        }

		internal void Popup()
		{
			if (isPopupShown) return;

			if (Manager.NativeManager.BeginPopupContextItem("##Popup"))
			{
				foreach(var item in menuItems)
				{
					item.Update();
				}

				Manager.NativeManager.EndPopup();
				isPopupShown = true;
			}
		}

		/// <summary>
		/// When renew, it is called.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void OnRenew(object sender, EventArgs e)
        {
            Renew();
        }

		void ReadSelect()
		{
			Func<Data.NodeBase, Utils.DelayedList<NodeTreeViewNode>, NodeTreeViewNode> search_node = null;
			search_node = (searched_node, treenodes) =>
			{
				if (search_node == null) return null;

				for (int i = 0; i < treenodes.Count; i++)
				{
					if (treenodes[i].node == searched_node) return treenodes[i];
					var ret = search_node(searched_node, treenodes[i].Children);
					if (ret != null) return ret;
				}
				return null;
			};

			var node = search_node(Core.SelectedNode, Children);

			SelectedNode = node;
		}

		void OnAfterSelect(object sender, EventArgs e)
		{
			ReadSelect();
		}
	}

    class NodeTreeViewNode : IControl
    {
        string id = "";

        public Data.NodeBase node { get; private set; } = null;

        internal Utils.DelayedList<NodeTreeViewNode> Children = new Utils.DelayedList<NodeTreeViewNode>();

		NodeTreeView treeView = null;

		public NodeTreeViewNode(NodeTreeView treeView, Data.NodeBase node, bool createChildren = false)
        {
            id = "###" + Manager.GetUniqueID().ToString();

			this.treeView = treeView;
            this.node = node;

            node.OnAfterAddNode += OnAfterAddNode;
            node.OnAfterRemoveNode += OnAfterRemoveNode;
            node.OnAfterExchangeNodes += OnAfterExchangeNodes;

            if (createChildren)
            {
                for(int i = 0; i < node.Children.Count; i++)
                {
                    var newNode = new NodeTreeViewNode(treeView, node.Children[i], true);
                    Children.Add(newNode);
                }
            }
        }

        public void RemoveEvent(bool recursion)
        {
            if (node is Data.Node)
            {
                var realNode = (Data.Node)node;
            }

            node.OnAfterAddNode -= OnAfterAddNode;
            node.OnAfterRemoveNode -= OnAfterRemoveNode;
            node.OnAfterExchangeNodes -= OnAfterExchangeNodes;
            if (recursion)
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    (Children[i]).RemoveEvent(true);
                }
            }
        }


        public void Update()
        {
			var flag = swig.TreeNodeFlags.OpenOnArrow | swig.TreeNodeFlags.OpenOnDoubleClick | swig.TreeNodeFlags.DefaultOpen;

			if(Core.SelectedNode == this.node)
			{
				flag = flag | swig.TreeNodeFlags.Selected;
			}

			if(this.node.Children.Count == 0)
			{
				flag = flag | swig.TreeNodeFlags.Leaf;
			}

			if (Manager.NativeManager.TreeNodeEx(node.Name + id, flag))
            {
				if(Manager.NativeManager.IsItemClicked(0) ||
					Manager.NativeManager.IsItemClicked(1))
				{
					Core.SelectedNode = this.node;
				}

				treeView.Popup();

				Children.Lock();

                foreach (var child in Children.Internal)
                {
                    child.Update();
                }

				Children.Unlock();

                Manager.NativeManager.TreePop();
            }
			else
			{
				treeView.Popup();
			}
        }

        public void ExpandAll()
        {
            Console.WriteLine("Not implemented.");
        }

        void OnAfterAddNode(object sender, ChangedValueEventArgs e)
        {
            var node = e.Value as Data.NodeBase;

            int ind = 0;
            for (; ind < node.Children.Count; ind++)
            {
                if (node == node.Children[ind]) break;
            }

            if (ind == node.Children.Count)
            {
                Children.Add(new NodeTreeViewNode(treeView ,node, true));
            }
            else
            {
                Children.Insert(ind, new NodeTreeViewNode(treeView, node, true));
            }

            ExpandAll();
        }

        void OnAfterRemoveNode(object sender, ChangedValueEventArgs e)
        {
            var node = e.Value as Data.NodeBase;

            for (int i = 0; i < Children.Count; i++)
            {
                var treenode = Children[i];
                if (treenode.node == node)
                {
                    treenode.RemoveEvent(true);
                    Children.Remove(treenode);
                    return;
                }
            }

            throw new Exception();
        }

        void OnAfterExchangeNodes(object sender, ChangedValueEventArgs e)
        {
            var node1 = (e.Value as Tuple<Data.Node, Data.Node>).Item1;
            var node2 = (e.Value as Tuple<Data.Node, Data.Node>).Item2;

            int ind1 = 0;
            int ind2 = 0;

            for (int i = 0; i < Children.Count; i++)
            {
                var treenode = Children[i];
                if (treenode.node == node1)
                {
                    ind1 = i;
                }
                if (treenode.node == node2)
                {
                    ind2 = i;
                }
            }

            if (ind1 > ind2)
            {
                var ind_temp = ind1;
                var node_temp = node1;
                ind1 = ind2;
                node1 = node2;
                ind2 = ind_temp;
                node2 = node_temp;
            }

            Children.Insert(ind2, new NodeTreeViewNode(treeView, node1, true));
            Children.RemoveAt(ind2 + 1);

            Children.Insert(ind1, new NodeTreeViewNode(treeView, node2, true));
            Children.RemoveAt(ind1 + 1);

            ExpandAll();
        }
    }
}