const fs = require('node:fs/promises');

async function main() {
  const expected = JSON.parse(await fs.readFile('wwwroot/seed-images/lifestyle-published-images.json','utf8'));
  const catalog = JSON.parse(await fs.readFile('wwwroot/seed-images/lifestyle-photo-catalog.json','utf8'));
  const base = 'https://mawja.premiumasp.net';
  const get = async route => {
    const response = await fetch(base + route,{signal:AbortSignal.timeout(30000)});
    if (!response.ok) throw new Error(`${route}: ${response.status}`);
    return response.json();
  };
  for (const slug of ['style-wave','mug-mood']) {
    const store = await get(`/api/Store/slug/${slug}`);
    const products = await get(`/api/Product/store/${store.id}`);
    const categories = await get(`/api/Category/store/${store.id}`);
    if (products.length !== 12 || categories.length !== 3) throw new Error(`${slug}: incorrect catalog counts`);
    if (store.logoUrl !== expected[`${slug}/store-logo`] || store.coverImageUrl !== expected[`${slug}/store-cover`]) throw new Error(`${slug}: branding mismatch`);
    let variants = 0;
    for (const product of products) {
      const key = `${slug}/${product.slug.slice(slug.length+1)}`;
      if (product.name !== catalog[key].Name || product.images.length !== 3) throw new Error(`${product.slug}: product mismatch`);
      if (product.thumbnailUrl !== expected[`${key}-1`]) throw new Error(`${product.slug}: thumbnail mismatch`);
      for (const image of product.images)
        if (image.url !== expected[`${key}-${image.displayOrder}`]) throw new Error(`${product.slug}: image mismatch`);
      if (product.variants.length !== catalog[key].VariantNames.length) throw new Error(`${product.slug}: variant count mismatch`);
      for (const variant of product.variants)
        if (variant.imageUrl !== catalog[key].VariantImageUrls[variant.sortOrder-1]) throw new Error(`${product.slug}: variant photo mismatch`);
      variants += product.variants.length;
    }
    for (const category of categories)
      if (category.imageUrl !== expected[`${slug}/category-${category.slug.slice(slug.length+1)}`]) throw new Error(`${slug}: category photo mismatch`);
    console.log(`${slug}: verified 12 products, 36 photos, 3 categories, ${variants} variants, logo and cover.`);
  }
}
main().catch(error => { console.error(error.message); process.exitCode=1; });
