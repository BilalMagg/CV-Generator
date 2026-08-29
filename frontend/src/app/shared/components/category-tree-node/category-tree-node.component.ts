import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TaxonomyNode } from '@app/models/category.model';

@Component({
  selector: 'app-category-tree-node',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './category-tree-node.component.html',
  styleUrl: './category-tree-node.component.scss',
})
export class CategoryTreeNodeComponent {
  @Input() node!: TaxonomyNode;
  @Input() selectedIds!: Set<string>;
  @Input() expandedIds!: Set<string>;
  @Input() mode: 'filter' | 'edit' = 'filter';

  @Output() selectionChange = new EventEmitter<string>();
  @Output() expandChange = new EventEmitter<string>();

  get hasChildren(): boolean {
    return !!this.node.children && this.node.children.length > 0;
  }

  onSelect(): void {
    this.selectionChange.emit(this.node.id);
  }

  onExpand(event: Event): void {
    event.stopPropagation();
    this.expandChange.emit(this.node.id);
  }
}
