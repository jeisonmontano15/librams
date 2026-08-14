/** A debounced function, plus a `cancel` so a pending call can be dropped on unmount. */
export type Debounced<T extends (...args: never[]) => void> = ((...args: Parameters<T>) => void) & {
  cancel: () => void;
};

export function debounce<T extends (...args: never[]) => void>(fn: T, delay: number): Debounced<T> {
  let timer: ReturnType<typeof setTimeout> | undefined;

  const debounced = (...args: Parameters<T>) => {
    if (timer !== undefined) clearTimeout(timer);
    timer = setTimeout(() => fn(...args), delay);
  };

  debounced.cancel = () => {
    if (timer !== undefined) clearTimeout(timer);
    timer = undefined;
  };

  return debounced;
}
